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
    /// Where the table ends up has been wrong three times over, each time for a different reason, so it is
    /// checked here with real entities on a real grid rather than by reasoning about angles.
    /// </summary>
    [Test]
    [TestCase(0f, 1f, Direction.South)]
    [TestCase(0f, -1f, Direction.North)]
    [TestCase(1f, 0f, Direction.West)]
    [TestCase(-1f, 0f, Direction.East)]
    [TestCase(0.2f, 1f, Direction.South)]
    [TestCase(-0.3f, 1f, Direction.South)]
    public async Task ATableIsPushedAwayFromWhoeverPushesIt(float offsetX, float offsetY, Direction expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var table = entManager.SpawnEntity("Table", map.GridCoords);
            var transform = entManager.System<SharedTransformSystem>();
            var where = transform.GetWorldPosition(table) + new Vector2(offsetX, offsetY);
            var user = entManager.SpawnEntity(null, new MapCoordinates(where, map.MapId));

            var comp = entManager.GetComponent<EmberProceduralTableComponent>(table);

            Assert.That(entManager.System<EmberTableFlipSystem>().TryGetFacing((table, comp), user, out var facing),
                Is.True);
            Assert.That(facing, Is.EqualTo(expected));
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
