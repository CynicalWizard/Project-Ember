using Content.Shared.Climbing.Components;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.Tests.Ember;

[TestFixture]
public sealed class WallFramePrototypeTest
{
    [Test]
    public async Task WallFrameBlocksMovementAndCanBeClimbed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        EntityUid wallFrame = default;

        await server.WaitPost(() =>
        {
            wallFrame = entityManager.SpawnEntity("WallFrame", map.GridCoords);
        });

        await server.WaitPost(() =>
        {
            Assert.That(entityManager.HasComponent<ClimbableComponent>(wallFrame), Is.True);

            var fixtures = entityManager.GetComponent<FixturesComponent>(wallFrame);
            Assert.That(fixtures.Fixtures.TryGetValue("fix1", out var fixture), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(fixture!.CollisionMask, Is.EqualTo((int) CollisionGroup.TableMask));
                Assert.That(fixture.CollisionLayer, Is.EqualTo((int) CollisionGroup.TableLayer));
            });
        });

        await pair.CleanReturnAsync();
    }
}
