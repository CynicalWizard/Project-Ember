#nullable enable
using System.Collections.Generic;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// How much a table takes is a fact about what it is plated with, not about which prototype happened to be
/// written first.
/// </summary>
/// <remarks>
/// Bay's <c>update_material</c>: half the material's integrity for the health, a tenth of its hardness for the
/// smallest hit that registers, and four times the damage if the plating is brittle and nothing sturdier holds
/// it. Before this, every table carried its own hand-typed numbers, which is how a stone table ended up weaker
/// than a wooden one and a brass table tougher than steel.
/// </remarks>
[TestFixture]
public sealed class EmberTableMaterialDamageTest
{
    [Test]
    public async Task EveryTableIsAsToughAsWhatItIsPlatedWith()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var map = await pair.CreateTestMap();

        var tableName = factory.GetComponentName<EmberProceduralTableComponent>();
        var problems = new List<string>();
        var checked_ = 0;

        foreach (var entity in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.Abstract || !entity.Components.TryGetComponent(tableName, out var raw))
                continue;

            var table = (EmberProceduralTableComponent) raw;

            var stats = EmberTableMaterialStats.For(
                Material(prototypes, table.Material),
                Material(prototypes, table.Reinforcement));

            var wanted = (int) MathF.Round(EmberTableMaterialStats.ThresholdFor(stats.Health));
            var id = entity.ID;
            var got = 0;
            var again = 0;

            await server.WaitPost(() =>
            {
                var uid = entities.SpawnEntity(id, map.GridCoords);
                got = Destruction(entities, uid);
                entities.DeleteEntity(uid);

                // The second one catches thresholds being scaled in place on something shared: a table that
                // grew tougher every time one was built would pass a test that only ever built one.
                var twice = entities.SpawnEntity(id, map.GridCoords);
                again = Destruction(entities, twice);
                entities.DeleteEntity(twice);
            });

            checked_++;

            if (got != wanted)
                problems.Add($"{id} is plated with {table.Material?.Id ?? "nothing"} and breaks at {got} rather than {wanted}");
            else if (again != got)
                problems.Add($"the second {id} to be built breaks at {again} rather than {got}");
        }

        Assert.That(checked_, Is.GreaterThan(0), "No procedural tables were found at all.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Bay's hardness floor, checked against the raw hit before anything else touches it: a steel table's is
    /// six, so a scratch does nothing at all while a real blow lands in full.
    /// </summary>
    [Test]
    public async Task AHitTooSmallForTheMaterialDoesNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();

        var scratched = 0f;
        var struck = 0f;

        await server.WaitPost(() =>
        {
            var damageable = entities.System<DamageableSystem>();
            var table = entities.SpawnEntity("Table", map.GridCoords);

            damageable.TryChangeDamage(table, Blunt(prototypes, 3), true);
            scratched = (float) entities.GetComponent<DamageableComponent>(table).TotalDamage;

            damageable.TryChangeDamage(table, Blunt(prototypes, 20), true);
            struck = (float) entities.GetComponent<DamageableComponent>(table).TotalDamage;

            entities.DeleteEntity(table);
        });

        Assert.Multiple(() =>
        {
            Assert.That(scratched, Is.Zero, "A steel table took damage from a hit under its hardness floor.");
            Assert.That(struck, Is.GreaterThan(0f), "A real blow bounced off as well.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Bay's <c>TABLE_BRITTLE_MATERIAL_MULTIPLIER</c>. Both tables resist damage the same way otherwise, so the
    /// whole of the difference between them is the glass.
    /// </summary>
    [Test]
    public async Task GlassGivesWayFourTimesAsFastAsSteel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();

        var steel = 0f;
        var glass = 0f;

        await server.WaitPost(() =>
        {
            var damageable = entities.System<DamageableSystem>();

            var steelTable = entities.SpawnEntity("Table", map.GridCoords);
            var glassTable = entities.SpawnEntity("TableGlass", map.GridCoords);

            // Not ignoring resistances: the brittle multiplier is one, and skipping them skips it.
            damageable.TryChangeDamage(steelTable, Blunt(prototypes, 10));
            damageable.TryChangeDamage(glassTable, Blunt(prototypes, 10));

            steel = (float) entities.GetComponent<DamageableComponent>(steelTable).TotalDamage;
            glass = (float) entities.GetComponent<DamageableComponent>(glassTable).TotalDamage;

            entities.DeleteEntity(steelTable);
            entities.DeleteEntity(glassTable);
        });

        Assert.That(steel, Is.GreaterThan(0f), "The steel table took nothing, so there is nothing to compare.");
        Assert.That(glass, Is.EqualTo(steel * EmberTableMaterialStats.BrittleMultiplier).Within(0.01f));

        await pair.CleanReturnAsync();
    }

    private static DamageSpecifier Blunt(IPrototypeManager prototypes, float amount)
    {
        return new DamageSpecifier(prototypes.Index<DamageTypePrototype>("Blunt"), amount);
    }

    private static EmberMaterialPrototype? Material(
        IPrototypeManager prototypes,
        ProtoId<EmberMaterialPrototype>? id)
    {
        return id is { } material && prototypes.TryIndex(material, out EmberMaterialPrototype? prototype)
            ? prototype
            : null;
    }

    private static int Destruction(IEntityManager entities, EntityUid uid)
    {
        if (!entities.TryGetComponent<DestructibleComponent>(uid, out var destructible))
            return 0;

        var highest = 0;
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && trigger.Damage > highest)
                highest = trigger.Damage;
        }

        return highest;
    }
}
