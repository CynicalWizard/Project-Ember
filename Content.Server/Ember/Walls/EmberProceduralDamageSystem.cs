using System.Diagnostics.CodeAnalysis;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;
using Content.Shared.Explosion.Components;
using Content.Shared.Radiation.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Walls;

/// <summary>
/// Gives a procedural wall the toughness of what it is made of, following Bay's <c>calculate_damage_data</c>:
/// integrity sets how much it takes to bring down, hardness sets the smallest hit that registers at all, and
/// armour divides everything that gets through. Radioactive materials irradiate their surroundings on top.
/// </summary>
public sealed class EmberProceduralDamageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralWallComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmberProceduralWallComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(Entity<EmberProceduralWallComponent> ent, ref MapInitEvent args)
    {
        if (!TryGetStats(ent.Comp, out var stats))
            return;

        // A wall's materials are fixed by its prototype — reinforcing one replaces the entity rather than
        // editing it — so this runs once per wall and the thresholds below never compound.
        ApplyIntegrity(ent, stats.Integrity);

        var resistance = EnsureComp<ExplosionResistanceComponent>(ent);
        _explosion.SetExplosionResistance(ent, stats.ExplosionCoefficient, resistance);

        if (stats.Radioactivity > 0f)
        {
            EnsureComp<RadiationSourceComponent>(ent).Intensity =
                stats.Radioactivity * EmberWallMaterialStats.RadiationIntensityScale;
        }
    }

    /// <summary>
    /// Bay expresses integrity as an absolute health pool. SS14 walls carry their own destruction thresholds, so
    /// scale those by how the material compares to steel and let the rest of the game keep its damage numbers.
    /// </summary>
    private void ApplyIntegrity(EntityUid uid, float integrity)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        var scale = integrity / EmberWallMaterialStats.ReferenceIntegrity;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger)
                trigger.Damage = Math.Max(1, (int) MathF.Round(trigger.Damage * scale));
        }
    }

    private void OnDamageModify(Entity<EmberProceduralWallComponent> ent, ref DamageModifyEvent args)
    {
        if (!TryGetStats(ent.Comp, out var stats))
            return;

        // Bay tests the raw hit against the hardness floor before armour touches it, so a weak hit is not merely
        // reduced, it is ignored outright.
        if ((float) args.Damage.GetTotal() < stats.MinimumDamage)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        Scale(args.Damage, BruteGroup, stats.BruteCoefficient);
        Scale(args.Damage, BurnGroup, stats.BurnCoefficient);
    }

    /// <summary>
    /// Reads the damage types out of the group prototype rather than listing them here, so a new brute type does
    /// not quietly start ignoring wall armour.
    /// </summary>
    private void Scale(DamageSpecifier damage, ProtoId<DamageGroupPrototype> groupId, float coefficient)
    {
        if (coefficient == 1f || !_prototype.TryIndex(groupId, out var group))
            return;

        foreach (var type in group.DamageTypes)
        {
            if (damage.DamageDict.TryGetValue(type, out var value))
                damage.DamageDict[type] = value * coefficient;
        }
    }

    private bool TryGetStats(EmberProceduralWallComponent component, out EmberWallStats stats)
    {
        stats = default;

        if (!TryGetPhysical(component.Material, out var material))
            return false;

        EmberMaterialPrototype? reinforcement = null;
        if (component.ReinforcementMaterial is { } reinforcementId)
            TryGetPhysical(reinforcementId, out reinforcement);

        stats = EmberWallMaterialStats.For(material, reinforcement);
        return true;
    }

    private bool TryGetPhysical(
        ProtoId<EmberWallMaterialPrototype>? id,
        [NotNullWhen(true)] out EmberMaterialPrototype? material)
    {
        material = null;

        return id is { } wallId &&
               !string.IsNullOrEmpty(wallId.Id) &&
               _prototype.TryIndex(wallId, out EmberWallMaterialPrototype? wall) &&
               wall.PhysicalMaterial is { } physical &&
               _prototype.TryIndex(physical, out material);
    }
}
