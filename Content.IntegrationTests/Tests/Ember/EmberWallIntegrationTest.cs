using Content.Shared.Ember.Walls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

[TestFixture]
public sealed class EmberWallIntegrationTest
{
    private static readonly string[] WallEntityIds =
    {
        "WallIron",
        "WallAluminium",
        "WallTitanium",
        "WallOsmium",
        "WallElectrum",
        "WallCopper",
        "WallBronze",
        "WallPlatinum",
        "WallMarble",
        "WallConcrete",
        "WallSilver",
        "WallWood",
        "WallStone",
        "WallReinforced",
        "WallPhoron"
    };

    [Test]
    public async Task EmberWallsCanBeSpawnedAndHaveValidProceduralWallComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            foreach (var wallId in WallEntityIds)
            {
                var wall = entityManager.SpawnEntity(wallId, map.GridCoords);

                Assert.That(entityManager.HasComponent<EmberProceduralWallComponent>(wall), Is.True, $"Entity {wallId} is missing EmberProceduralWallComponent!");
                var procWall = entityManager.GetComponent<EmberProceduralWallComponent>(wall);

                Assert.That(protoManager.HasIndex<EmberWallMaterialPrototype>(procWall.Material), Is.True, $"Material {procWall.Material} for wall {wallId} not found in PrototypeManager!");
            }
        });

        await pair.CleanReturnAsync();
    }
}
