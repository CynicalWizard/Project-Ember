using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared.Ember.Structures;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A light standing against a wall has to be pushed into it, by an amount that depends on which wall.
/// </summary>
/// <remarks>
/// Walls are drawn tall rather than flat, so a fitting on the far wall of a room rides up its face while one on
/// the near wall sits against a thin edge and needs nothing. Bay's own numbers, in
/// <c>/obj/machinery/light/on_update_icon</c>: <c>pixel_y = 21</c> towards a wall to the north, <c>pixel_x</c>
/// of plus or minus 10 towards one to either side, nothing for one to the south. It moves towards its wall,
/// never merely upwards. It only applies them when the tile it looks at is actually dense, and so does this — a
/// light on a pole in the middle of a room would otherwise stand a fifth of a tile away from where it is.
///
/// The wall goes up after the light on purpose. That is the order a map loads in, and the first version of this
/// passed only because it reached in and recomputed the offset by hand — which nothing in a round does, so every
/// light already on a station stayed where it was while freshly built ones came out right.
/// </remarks>
[TestFixture]
public sealed class EmberWallFixtureOffsetTest
{
    // Where the wall is for a light facing each way, and how far into it the sprite should go.
    private static readonly (int Degrees, string Facing, Direction Wall, float Depth)[] Cases =
    {
        (0, "south", Direction.North, 21f / 32f),
        (180, "north", Direction.South, 0f),
        (90, "east", Direction.West, 10f / 32f),
        (270, "west", Direction.East, 10f / 32f),
    };

    [Test]
    public async Task ALightSinksIntoTheWallBehindItAndOnlyIfThereIsOne()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var problems = new List<string>();

        foreach (var (degrees, facing, wall, depth) in Cases)
        {
            EntityUid light = default;
            EntityUid wallEntity = default;

            await server.WaitPost(() =>
            {
                Floor(server, serverEnts, map);

                light = serverEnts.SpawnEntity("PoweredlightEmpty", new EntityCoordinates(map.Grid, 0, 0));
                transform.SetLocalRotation(light, Angle.FromDegrees(degrees));
            });

            await pair.RunTicksSync(15);

            // With nothing behind it, it stays where it is.
            await pair.Client.WaitPost(() =>
            {
                var sprite = clientEnts.GetComponent<SpriteComponent>(
                    clientEnts.GetEntity(serverEnts.GetNetEntity(light)));

                if (sprite.Offset != Vector2.Zero)
                    problems.Add($"facing {facing} with open floor behind it, it moved to {sprite.Offset}");
            });

            // Now put a wall there.
            await server.WaitPost(() =>
            {
                wallEntity = serverEnts.SpawnEntity("WallSolid",
                    new EntityCoordinates(map.Grid, wall.ToVec().X, wall.ToVec().Y));
            });

            await pair.RunTicksSync(15);

            await pair.Client.WaitPost(() =>
            {
                var clientLight = clientEnts.GetEntity(serverEnts.GetNetEntity(light));
                var sprite = clientEnts.GetComponent<SpriteComponent>(clientLight);

                // The offset is kept in the sprite's own frame, which the light turns, so it has to be turned
                // back to say anything about the world. Out there it has to point at the wall, like Bay's does.
                var inTheWorld = Angle.FromDegrees(degrees).RotateVec(sprite.Offset);
                var wanted = wall.ToVec() * depth;

                if (!MathHelper.CloseTo(inTheWorld.X, wanted.X, 0.01f) ||
                    !MathHelper.CloseTo(inTheWorld.Y, wanted.Y, 0.01f))
                {
                    problems.Add($"facing {facing} against a wall to the {wall}, it leans {inTheWorld} rather than {wanted}");
                }
            });

            await server.WaitPost(() =>
            {
                serverEnts.DeleteEntity(light);
                serverEnts.DeleteEntity(wallEntity);
            });
        }

        Assert.That(problems, Is.Empty, string.Join(NewLine, problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Turning the camera turns which wall is the far one, so how deep the lean is has to follow it — though not
    /// which way it goes, since the wall stays where it was and so does the frame the offset is written in.
    /// </summary>
    [Test]
    public async Task TurningTheCameraChangesHowDeepALightLeans()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid light = default;

        await server.WaitPost(() =>
        {
            Floor(server, serverEnts, map);

            // Facing south with its wall to the north: the deepest lean there is.
            light = serverEnts.SpawnEntity("PoweredlightEmpty", new EntityCoordinates(map.Grid, 0, 0));
            serverEnts.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 0, 1));
        });

        await pair.RunTicksSync(15);

        var problems = new List<string>();

        // A quarter turn of the camera puts that wall on the side of the screen, where the lean is shallower,
        // and half a turn puts it at the bottom, where there is nothing to climb.
        foreach (var (degrees, expected) in new[] { (0, 21f / 32f), (90, 10f / 32f), (180, 0f), (270, 10f / 32f) })
        {
            await pair.Client.WaitPost(() =>
                pair.Client.ResolveDependency<IEyeManager>().CurrentEye.Rotation = Angle.FromDegrees(degrees));

            await pair.RunTicksSync(5);

            await pair.Client.WaitPost(() =>
            {
                var sprite = clientEnts.GetComponent<SpriteComponent>(
                    clientEnts.GetEntity(serverEnts.GetNetEntity(light)));

                // However far it leans, it leans at the wall, which is to the north whatever the camera does.
                var wanted = Direction.North.ToVec() * expected;

                if (!MathHelper.CloseTo(sprite.Offset.X, wanted.X, 0.01f) ||
                    !MathHelper.CloseTo(sprite.Offset.Y, wanted.Y, 0.01f))
                {
                    problems.Add($"with the camera at {degrees}, it leans {sprite.Offset} rather than {wanted}");
                }
            });
        }

        await pair.Client.WaitPost(() =>
            pair.Client.ResolveDependency<IEyeManager>().CurrentEye.Rotation = Angle.Zero);

        Assert.That(problems, Is.Empty, string.Join(NewLine, problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A light that has leant into a wall is no longer standing on its own tile, and the tile it landed on is one
    /// both of the rooms either side can see. Bay gets this for free — BYOND draws nothing standing on a turf you
    /// cannot see — so the room next door has to be made to lose it here.
    /// </summary>
    [Test]
    public async Task ALightSunkIntoAWallIsNotVisibleFromTheOtherSideOfIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var eyes = pair.Client.ResolveDependency<IEyeManager>();
        var map = await pair.CreateTestMap();

        var problems = new List<string>();

        // Sunk into the wall to its north, and one that leans nowhere and so has nothing to hide.
        foreach (var (degrees, wall, hides) in new[]
                 {
                     (0, Direction.North, true),
                     (180, Direction.South, false),
                 })
        {
            EntityUid light = default;
            EntityUid wallEntity = default;

            await server.WaitPost(() =>
            {
                Floor(server, serverEnts, map);

                light = serverEnts.SpawnEntity("PoweredlightEmpty", new EntityCoordinates(map.Grid, 0, 0));
                transform.SetLocalRotation(light, Angle.FromDegrees(degrees));
                wallEntity = serverEnts.SpawnEntity("WallSolid",
                    new EntityCoordinates(map.Grid, wall.ToVec().X, wall.ToVec().Y));
            });

            await pair.RunTicksSync(15);

            var here = default(MapCoordinates);
            await server.WaitPost(() => here = transform.GetMapCoordinates(light));

            // Standing in the light's own room, then three tiles past the wall, in the room beyond it.
            foreach (var (where, across) in new[] { (here, false), (Offset(here, wall, 3), true) })
            {
                await pair.Client.WaitPost(() => ((Eye) eyes.CurrentEye).Position = where);

                await pair.RunTicksSync(5);

                await pair.Client.WaitPost(() =>
                {
                    var sprite = clientEnts.GetComponent<SpriteComponent>(
                        clientEnts.GetEntity(serverEnts.GetNetEntity(light)));

                    var wanted = !(across && hides);

                    if (sprite.Visible != wanted)
                    {
                        problems.Add($"a light with its wall to the {wall}, seen from " +
                                     $"{(across ? "past that wall" : "its own room")}, was " +
                                     $"{(sprite.Visible ? "drawn" : "hidden")}");
                    }
                });
            }

            await server.WaitPost(() =>
            {
                serverEnts.DeleteEntity(light);
                serverEnts.DeleteEntity(wallEntity);
            });
        }

        await pair.Client.WaitPost(() => ((Eye) eyes.CurrentEye).Position = MapCoordinates.Nullspace);

        Assert.That(problems, Is.Empty, string.Join(NewLine, problems));

        await pair.CleanReturnAsync();
    }

    private static MapCoordinates Offset(MapCoordinates from, Direction direction, float tiles)
    {
        return new MapCoordinates(from.Position + direction.ToVec() * tiles, from.MapId);
    }

    private static void Floor(RobustIntegrationTest.ServerIntegrationInstance server, IEntityManager entities, TestMapData map)
    {
        var maps = entities.System<SharedMapSystem>();
        var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

        for (var x = -2; x <= 2; x++)
        for (var y = -4; y <= 4; y++)
        {
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), tile);
        }
    }

    private const string NewLine = "\n";
}
