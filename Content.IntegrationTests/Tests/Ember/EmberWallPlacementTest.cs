using System.Collections.Generic;
using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A tile holds a wall or a low wall, never both. Nothing in SS14 enforces that on its own: a low wall sits on
/// the table layer so it can be climbed and shot over, which is exactly the layer TileNotBlocked does not look
/// at, so a wall could be raised straight through one.
/// </summary>
[TestFixture]
public sealed class EmberWallPlacementTest
{
    [Test]
    public async Task WallsAndLowWallsRefuseEachOthersTiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();

        var problems = new List<string>();

        await server.WaitPost(() =>
        {
            var user = entManager.SpawnEntity(null, map.GridCoords);

            foreach (var occupant in new[] { "WallSolid", "WallFrame" })
            {
                var standing = entManager.SpawnEntity(occupant, map.GridCoords);

                foreach (var recipeId in new[] { "Wall", "WallFrame", "Girder" })
                {
                    var recipe = protoManager.Index<ConstructionPrototype>(recipeId);

                    if (!recipe.Conditions.Any(condition => condition is EmberNoWallInTile))
                    {
                        problems.Add($"{recipeId} does not check for a wall already on the tile");
                        continue;
                    }

                    foreach (var condition in recipe.Conditions)
                    {
                        if (condition is not EmberNoWallInTile)
                            continue;

                        if (condition.Condition(user, map.GridCoords, Direction.South))
                            problems.Add($"{recipeId} may be built on a tile already holding {occupant}");
                    }
                }

                entManager.DeleteEntity(standing);
            }

            entManager.DeleteEntity(user);
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The condition has to let an empty tile through, or nothing could be built at all.
    /// </summary>
    [Test]
    public async Task AnEmptyTileStillAcceptsAWall()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var user = entManager.SpawnEntity(null, map.GridCoords);
            var condition = new EmberNoWallInTile();

            Assert.That(condition.Condition(user, map.GridCoords, Direction.South), Is.True);

            entManager.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Windows and grilles are meant to share a tile with a low wall, so the check must be about walls only.
    /// </summary>
    [Test]
    public async Task ALowWallStillAcceptsWhatIsBuiltIntoIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var grille = entManager.SpawnEntity("Grille", map.GridCoords);

            Assert.That(entManager.HasComponent<EmberProceduralWallComponent>(grille), Is.False);
            Assert.That(
                entManager.TryGetComponent(grille, out EmberProceduralStructureComponent? structure) &&
                structure.Role == EmberProceduralStructureRole.WallFrame,
                Is.False,
                "A grille counts as a low wall, so nothing could be built alongside one.");

            entManager.DeleteEntity(grille);
        });

        await pair.CleanReturnAsync();
    }
}
