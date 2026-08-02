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
    /// A grille goes into a low wall, so its check has to refuse a full wall while letting a low wall through.
    /// It used to get that outcome by accident, through the very hole this fixture closes.
    /// </summary>
    [Test]
    public async Task GrillesGoIntoLowWallsButNotThroughWalls()
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

            foreach (var recipe in protoManager.EnumeratePrototypes<ConstructionPrototype>())
            {
                if (!recipe.ID.StartsWith("Grille"))
                    continue;

                var condition = recipe.Conditions.OfType<EmberNoWallInTile>().FirstOrDefault();
                if (condition == null)
                {
                    problems.Add($"{recipe.ID} does not say whether it may go through a wall");
                    continue;
                }

                var lowWall = entManager.SpawnEntity("WallFrame", map.GridCoords);
                if (!condition.Condition(user, map.GridCoords, Direction.South))
                    problems.Add($"{recipe.ID} refuses a low wall, which is where a grille belongs");
                entManager.DeleteEntity(lowWall);

                var wall = entManager.SpawnEntity("WallSolid", map.GridCoords);
                if (condition.Condition(user, map.GridCoords, Direction.South))
                    problems.Add($"{recipe.ID} may be built through a wall");
                entManager.DeleteEntity(wall);
            }

            entManager.DeleteEntity(user);
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }
}
