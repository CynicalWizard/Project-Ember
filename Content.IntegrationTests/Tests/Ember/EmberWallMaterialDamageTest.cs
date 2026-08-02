using System.Collections.Generic;
using System.Linq;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Projectiles;
using Content.Server.Radiation.Components;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;
using Content.Shared.Radiation.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A wall's toughness is derived from its material at map init, so nothing in the prototypes says what a given
/// wall should end up with. These check the derivation against the same Bay formulas the sprites were ported
/// from, on the real prototypes rather than on stand-ins.
/// </summary>
[TestFixture]
public sealed class EmberWallMaterialDamageTest
{
    [Test]
    public async Task WallToughnessScalesWithItsMaterial()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var componentFactory = server.ResolveDependency<IComponentFactory>();
        var map = await pair.CreateTestMap();

        var wallName = componentFactory.GetComponentName<EmberProceduralWallComponent>();
        var problems = new List<string>();

        await server.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.Components.TryGetComponent(wallName, out var rawWall))
                    continue;

                if (!proto.Components.TryGetComponent("Destructible", out var rawDestructible))
                    continue;

                var declared = Thresholds((DestructibleComponent) rawDestructible);
                if (declared.Count == 0)
                    continue;

                if (!TryGetStats(protoManager, (EmberProceduralWallComponent) rawWall, out var stats))
                {
                    problems.Add($"{proto.ID} has no physical material behind {((EmberProceduralWallComponent) rawWall).Material}");
                    continue;
                }

                var wall = entManager.SpawnEntity(proto.ID, map.GridCoords);
                var actual = Thresholds(entManager.GetComponent<DestructibleComponent>(wall));

                var scale = stats.Integrity / EmberWallMaterialStats.ReferenceIntegrity;

                for (var i = 0; i < declared.Count; i++)
                {
                    var expected = Math.Max(1, (int) MathF.Round(declared[i] * scale));
                    if (actual[i] != expected)
                        problems.Add($"{proto.ID} threshold {i} is {actual[i]}, expected {expected}");
                }

                entManager.DeleteEntity(wall);
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Bay's hardness floor: a hit that lands under it is ignored outright rather than merely reduced, which is
    /// what stops anything sharp from eventually chewing through a bulkhead.
    /// </summary>
    [Test]
    public async Task HitsUnderTheHardnessFloorDoNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var damageable = server.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        var blunt = protoManager.Index<DamageTypePrototype>("Blunt");
        var appearanceSystem = server.System<SharedAppearanceSystem>();

        await server.WaitPost(() =>
        {
            // Steel: hardness 60, so anything under round(60 * 2.6 / 10) = 16 bounces off.
            var wall = entManager.SpawnEntity("WallSolid", map.GridCoords);
            var damage = entManager.GetComponent<DamageableComponent>(wall);

            damageable.TryChangeDamage(wall, new DamageSpecifier(blunt, 15));
            Assert.That((float) damage.TotalDamage, Is.Zero,
                "A hit under the hardness floor still damaged the wall.");

            damageable.TryChangeDamage(wall, new DamageSpecifier(blunt, 100));

            var taken = (float) damage.TotalDamage;
            Assert.That(taken, Is.GreaterThan(0f), "A solid hit did nothing.");
            Assert.That(taken, Is.LessThan(100f), "Wall armour absorbed nothing.");

            // How battered the wall looks is published for the client, which cannot see the threshold itself.
            Assert.That(
                appearanceSystem.TryGetData<float>(wall, EmberWallVisuals.DamageFraction, out var fraction),
                Is.True,
                "A damaged wall published no damage fraction, so it will never show an overlay.");
            Assert.That(fraction, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));

            entManager.DeleteEntity(wall);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The floor is compared against the raw hit, which is what Bay does, and it is easy to get wrong: measured
    /// after the wall's damage modifier set, a rifle round loses ten points to a flat reduction and lands under
    /// the floor, leaving steel walls immune to gunfire.
    /// </summary>
    [Test]
    public async Task RifleRoundsStillDamageASteelWall()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var damageable = server.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        var bullet = protoManager.Index<EntityPrototype>("BulletLightRifle");
        var projectile = (ProjectileComponent) bullet.Components["Projectile"].Component;

        await server.WaitPost(() =>
        {
            var wall = entManager.SpawnEntity("WallSolid", map.GridCoords);
            var damage = entManager.GetComponent<DamageableComponent>(wall);

            damageable.TryChangeDamage(wall, projectile.Damage);

            Assert.That((float) damage.TotalDamage, Is.GreaterThan(0f),
                $"A rifle round ({projectile.Damage.GetTotal()} raw) did nothing to a steel wall.");

            entManager.DeleteEntity(wall);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Uranium's radioactivity was ported into the material prototypes but nothing read it, so a uranium
    /// bulkhead was exactly as safe to stand next to as a steel one.
    /// </summary>
    [Test]
    public async Task RadioactiveMaterialsMakeTheirWallsRadioactive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var uranium = entManager.SpawnEntity("WallUranium", map.GridCoords);
            var steel = entManager.SpawnEntity("WallSolid", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entManager.TryGetComponent(uranium, out RadiationSourceComponent? source), Is.True,
                    "A uranium wall emits no radiation.");

                // The gridcast subtracts every blocker on the way out, the wall's own included, so the source
                // has to carry that back or nothing escapes the tile it sits on.
                var blocked = entManager.TryGetComponent(uranium, out RadiationBlockerComponent? blocker)
                    ? blocker.RadResistance
                    : 0f;

                Assert.That(source!.Intensity - blocked,
                    Is.EqualTo(12f * EmberWallMaterialStats.RadiationIntensityScale).Within(0.001f),
                    "A uranium wall shields away its own radiation.");

                Assert.That(entManager.HasComponent<RadiationSourceComponent>(steel), Is.False,
                    "An inert material gave its wall a radiation source.");
            });

            entManager.DeleteEntity(uranium);
            entManager.DeleteEntity(steel);
        });

        await pair.CleanReturnAsync();
    }

    private static List<int> Thresholds(DestructibleComponent destructible)
    {
        return destructible.Thresholds
            .Select(threshold => threshold.Trigger as DamageTrigger)
            .Where(trigger => trigger != null)
            .Select(trigger => trigger!.Damage)
            .ToList();
    }

    private static bool TryGetStats(
        IPrototypeManager protoManager,
        EmberProceduralWallComponent wall,
        out EmberWallStats stats)
    {
        stats = default;

        if (!TryGetPhysical(protoManager, wall.Material, out var material))
            return false;

        EmberMaterialPrototype? reinforcement = null;
        if (wall.ReinforcementMaterial is { } reinforcementId)
            TryGetPhysical(protoManager, reinforcementId, out reinforcement);

        stats = EmberWallMaterialStats.For(material, reinforcement);
        return true;
    }

    private static bool TryGetPhysical(
        IPrototypeManager protoManager,
        ProtoId<EmberWallMaterialPrototype> id,
        out EmberMaterialPrototype? material)
    {
        material = null;

        return !string.IsNullOrEmpty(id.Id) &&
               protoManager.TryIndex(id, out EmberWallMaterialPrototype? wall) &&
               wall.PhysicalMaterial is { } physical &&
               protoManager.TryIndex(physical, out material);
    }
}
