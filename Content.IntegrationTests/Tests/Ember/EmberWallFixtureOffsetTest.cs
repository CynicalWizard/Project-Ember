using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Ember.Structures;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A light standing against a wall has to be pushed into it, by an amount that depends on which wall.
/// </summary>
/// <remarks>
/// Walls are drawn tall rather than flat, so a fitting on the far wall of a room rides up its face while one on
/// the near wall sits against a thin edge and needs nothing. Bay's own numbers: 21 pixels into a wall to the
/// north, 10 into one to either side, nothing for one to the south. It only applies them when the tile it
/// looks at is actually dense, and so does this — a light on a pole in the middle of a room would otherwise
/// stand a fifth of a tile away from where it is.
///
/// The wall goes up after the light on purpose. That is the order a map loads in, and the first version of this
/// passed only because it reached in and recomputed the offset by hand — which nothing in a round does, so every
/// light already on a station stayed where it was while freshly built ones came out right.
///
/// Which wall is the far one is a question about the screen, and the walls already answer it that way: turn the
/// view and their pictures change. A fitting that answered it about the grid instead leant the wrong way the
/// moment anyone pressed NUM 4.
/// </remarks>
[TestFixture]
public sealed class EmberWallFixtureOffsetTest
{
    // How far the sprite should sink, by the way the light faces. The wall is behind it.
    private static readonly (int Degrees, string Facing, float Depth)[] Cases =
    {
        (0, "south", 21f / 32f),
        (180, "north", 0f),
        (90, "east", 10f / 32f),
        (270, "west", 10f / 32f),
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

        foreach (var (degrees, facing, depth) in Cases)
        {
            EntityUid light = default;
            EntityUid wall = default;

            await server.WaitPost(() =>
            {
                var maps = serverEnts.System<SharedMapSystem>();
                var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), tile);
                }

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
                var behind = serverEnts.GetComponent<TransformComponent>(light).LocalRotation
                    .GetCardinalDir().GetOpposite();

                wall = serverEnts.SpawnEntity("WallSolid",
                    new EntityCoordinates(map.Grid, behind.ToVec().X, behind.ToVec().Y));
            });

            await pair.RunTicksSync(15);

            await pair.Client.WaitPost(() =>
            {
                var clientLight = clientEnts.GetEntity(serverEnts.GetNetEntity(light));
                var sprite = clientEnts.GetComponent<SpriteComponent>(clientLight);

                // The offset is kept in the sprite's own frame, which the light turns; on screen, where the
                // three-quarter view lives, it has to come out pointing straight up into the far wall.
                var onScreen = Angle.FromDegrees(degrees).RotateVec(sprite.Offset);

                if (!MathHelper.CloseTo(onScreen.Y, depth, 0.01f) ||
                    !MathHelper.CloseTo(onScreen.X, 0f, 0.01f))
                {
                    problems.Add($"facing {facing} against a wall, it leans {onScreen} rather than 0,{depth}");
                }
            });

            await server.WaitPost(() =>
            {
                serverEnts.DeleteEntity(light);
                serverEnts.DeleteEntity(wall);
            });
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Turning the camera turns which wall is the far one, so the lean has to follow it.
    /// </summary>
    [Test]
    public async Task TurningTheCameraTurnsWhichWayALightLeans()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid light = default;

        await server.WaitPost(() =>
        {
            var maps = serverEnts.System<SharedMapSystem>();
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), tile);
            }

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

                // Whatever the offset is in the sprite's own frame, on screen it has to point straight up by
                // however much that wall is worth.
                var onScreen = Angle.FromDegrees(degrees).RotateVec(sprite.Offset);

                if (!MathHelper.CloseTo(onScreen.Y, expected, 0.01f) ||
                    !MathHelper.CloseTo(onScreen.X, 0f, 0.01f))
                {
                    problems.Add($"with the camera at {degrees}, it leans {onScreen} rather than 0,{expected}");
                }
            });
        }

        await pair.Client.WaitPost(() =>
            pair.Client.ResolveDependency<IEyeManager>().CurrentEye.Rotation = Angle.Zero);

        Assert.That(problems, Is.Empty, string.Join(NewLine, problems));

        await pair.CleanReturnAsync();
    }

    private const string NewLine = "\n";
}
