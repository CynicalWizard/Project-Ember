using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Content.Shared.Climbing.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A Bay table is drawn from whatever it is plated with, so the material has to name a set of corner sprites that
/// exists. A material naming a set that was never drawn produces a table you can walk into and cannot see.
/// </summary>
[TestFixture]
public sealed class EmberTableTest
{
    [Test]
    public async Task EveryTableIsDrawnFromSpritesThatExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var resourceManager = pair.Server.ResolveDependency<IResourceManager>();

        var tableName = componentFactory.GetComponentName<EmberProceduralTableComponent>();
        var states = new Dictionary<ResPath, HashSet<string>>();
        var problems = new List<string>();
        var found = 0;

        foreach (var entity in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.Abstract || !entity.Components.TryGetComponent(tableName, out var raw))
                continue;

            var table = (EmberProceduralTableComponent) raw;

            if (table.Material is not { } materialId)
                continue;

            found++;

            if (!protoManager.TryIndex(materialId, out EmberMaterialPrototype? material))
            {
                problems.Add($"{entity.ID} is plated with {materialId}, which is not a material");
                continue;
            }

            if (!states.TryGetValue(table.Sprite, out var available))
            {
                available = ReadStates(resourceManager, table.Sprite);
                states[table.Sprite] = available;
            }

            // Corner 7 is the fully surrounded one, which every set has to have or a table in the middle of a
            // block would show a hole.
            var plating = EmberProceduralTableVisuals.CornerState(
                EmberProceduralTableVisuals.PlatingStateBase(material), 7);

            if (!available.Contains(plating))
                problems.Add($"{entity.ID} is plated with {materialId}, which draws as '{plating}' — not in {table.Sprite}");

            if (table.Carpeted && !available.Contains(
                    EmberProceduralTableVisuals.CornerState(EmberProceduralTableVisuals.CarpetStateBase, 7)))
            {
                problems.Add($"{entity.ID} is carpeted, but {table.Sprite} has no felt to draw");
            }
        }

        Assert.That(found, Is.GreaterThan(0), "No plated tables were found at all.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }


    /// <summary>
    /// A table on its side is cover: it stands in a lip along the edge it faces, it stops bullets rather than
    /// letting them over, and there is nothing left to climb. Every one of those was wrong first time round, so
    /// they are checked rather than believed.
    /// </summary>
    [Test]
    public async Task ATippedTableIsCover()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var table = entManager.SpawnEntity("Table", map.GridCoords);
            var flip = entManager.System<EmberTableFlipSystem>();
            var fixtures = entManager.System<FixtureSystem>();
            var comp = entManager.GetComponent<EmberProceduralTableComponent>(table);

            Assert.That(entManager.HasComponent<ClimbableComponent>(table), Is.True,
                "A table you cannot climb was upright to begin with.");

            flip.SetFlipped((table, comp), Direction.North, true);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Flipped, Is.True);
                Assert.That(comp.FlipFacing, Is.EqualTo(Direction.North),
                    "It did not go over the way it was pushed.");
                Assert.That(entManager.HasComponent<ClimbableComponent>(table), Is.False,
                    "You can still climb onto a table lying on its side.");
                Assert.That(entManager.HasComponent<RequireProjectileTargetComponent>(table), Is.False,
                    "Bullets still pass over a table being used as cover.");

                var fixture = fixtures.GetFixtureOrNull(table, "fix1");
                Assert.That(fixture, Is.Not.Null, "It lost its collision entirely.");
                var lip = fixture!.Shape.ComputeAABB(new Transform(Vector2.Zero, 0f), 0);
                Assert.That(lip.Height, Is.LessThan(0.5f),
                    "It still takes up the whole tile rather than one edge of it.");
                Assert.That(lip.Center.Y, Is.GreaterThan(0.25f),
                    "It blocks the wrong edge of its tile for the way it was pushed.");

                // Having a fixture is not the same as colliding: taking the last one off a body switches its
                // collision off, and putting one back does not switch it on again.
                Assert.That(entManager.GetComponent<PhysicsComponent>(table).CanCollide, Is.True,
                    "It has a shape but collides with nothing.");
            });

            flip.SetFlipped((table, comp), Direction.North, false);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Flipped, Is.False);
                Assert.That(entManager.HasComponent<ClimbableComponent>(table), Is.True);
                Assert.That(entManager.HasComponent<RequireProjectileTargetComponent>(table), Is.True);

                var fixture = fixtures.GetFixtureOrNull(table, "fix1");
                Assert.That(fixture, Is.Not.Null);
                Assert.That(fixture!.Shape.ComputeAABB(new Transform(Vector2.Zero, 0f), 0).Height,
                    Is.GreaterThan(0.5f), "Standing it back up left it the size of its own edge.");
                Assert.That(entManager.GetComponent<PhysicsComponent>(table).CanCollide, Is.True,
                    "Standing it back up left it collided with nothing.");
            });
        });

        await pair.CleanReturnAsync();
    }


    /// <summary>
    /// Wherever the pusher is standing, the table goes over away from them — and goes over the same way from
    /// anywhere on the same tile.
    /// </summary>
    /// <remarks>
    /// The second half is the part that was wrong. The direction used to be read off the two world positions,
    /// and a player does not stand on the middle of their tile: anywhere near the line running through the
    /// table, a few pixels either way swung the answer between two edges, and standing on the table it swung
    /// between all four. From the player's side that is a table which goes over the right way, the wrong way or
    /// refuses, on nothing they can see. So this sweeps a whole tile's worth of standing spots per tile and
    /// insists the answer never changes within one of them.
    ///
    /// A single table, because a row is only allowed over along its own line and the refusals would drown out
    /// what is being measured here; the row rule has its own test.
    /// </remarks>
    [Test]
    public async Task ATableIsPushedAwayFromWhoeverPushesItAndTheSameWayFromAnywhereOnATile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var wrongWay = new List<string>();
        var unstable = new List<string>();

        await server.WaitPost(() =>
        {
            var maps = entManager.System<SharedMapSystem>();
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, x, y), tile);
            }

            var table = entManager.SpawnEntity("Table", new EntityCoordinates(map.Grid, 0, 0));
            var comp = entManager.GetComponent<EmberProceduralTableComponent>(table);
            var flip = entManager.System<EmberTableFlipSystem>();
            var user = entManager.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0, -2));
            var centre = transform.GetWorldPosition(table);

            // Middle and both edges of each tile, along each axis: where a player actually ends up standing.
            const float step = 0.4f;

            foreach (var (tileX, tileY) in Surrounding())
            {
                Direction? agreed = null;

                for (var subX = -1; subX <= 1; subX++)
                for (var subY = -1; subY <= 1; subY++)
                {
                    var offset = new Vector2(tileX + subX * step, tileY + subY * step);
                    transform.SetWorldPosition(user, centre + offset);

                    if (!flip.TryGetFacing((table, comp), user, out var facing))
                    {
                        unstable.Add($"standing at ({tileX},{tileY}) offset {offset} it refused to go over");
                        continue;
                    }

                    if (Vector2.Dot(-offset, facing.ToVec()) <= 0f)
                        wrongWay.Add($"standing at {offset} pushed it {facing}, which is towards them");

                    agreed ??= facing;

                    if (agreed != facing)
                    {
                        unstable.Add(
                            $"on the tile at ({tileX},{tileY}) it goes {agreed} from one spot and {facing} "
                            + $"from {offset}");
                    }

                    flip.SetFlipped((table, comp), facing, false);
                }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(wrongWay, Is.Empty, string.Join("\n", wrongWay));
            Assert.That(unstable, Is.Empty, string.Join("\n", unstable));
        });

        await pair.CleanReturnAsync();

        // The eight tiles touching the table, and the four one step further out along the compass points.
        static IEnumerable<(int X, int Y)> Surrounding()
        {
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                if (x != 0 || y != 0)
                    yield return (x, y);
            }

            yield return (0, 2);
            yield return (0, -2);
            yield return (2, 0);
            yield return (-2, 0);
        }
    }

    /// <summary>
    /// A table you are standing on has no away to go over in, so it does not go over.
    /// </summary>
    /// <remarks>
    /// Climbing onto tables is ordinary, and the direction used to come out of the difference between two world
    /// positions — which on the same tile is nearly zero, so a hair of a step decided between all four edges.
    /// Bay refuses this case, by accident of asking which turf you are on; refusing it on purpose says the same
    /// thing and says it out loud.
    /// </remarks>
    [Test]
    public async Task ATableYouAreStandingOnDoesNotGoOver()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var table = entManager.SpawnEntity("Table", map.GridCoords);
            var comp = entManager.GetComponent<EmberProceduralTableComponent>(table);
            var user = entManager.SpawnEntity("MobHuman", map.GridCoords);

            transform.SetWorldPosition(user, transform.GetWorldPosition(table));

            Assert.That(entManager.System<EmberTableFlipSystem>().TryGetFacing((table, comp), user, out _),
                Is.False, "It went over while somebody was standing on it, in a direction picked out of the air.");
        });

        await pair.CleanReturnAsync();
    }

    private static HashSet<string> ReadStates(IResourceManager resourceManager, ResPath rsi)
    {
        var names = new HashSet<string>();
        var path = rsi / "meta.json";

        if (!resourceManager.TryContentFileRead(path, out var stream))
            return names;

        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("states", out var states))
            return names;

        foreach (var state in states.EnumerateArray())
        {
            if (state.TryGetProperty("name", out var name) && name.GetString() is { } value)
                names.Add(value);
        }

        return names;
    }
}
