using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Ember.Structures;
using Robust.Client.GameObjects;
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

                // Straight up in the sprite's own frame is straight into the wall, whichever wall that is.
                if (!MathHelper.CloseTo(sprite.Offset.Y, depth, 0.001f) ||
                    !MathHelper.CloseTo(sprite.Offset.X, 0f, 0.001f))
                {
                    problems.Add($"facing {facing} against a wall, it sank {sprite.Offset} rather than 0,{depth}");
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
}
