using System.Diagnostics.CodeAnalysis;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Server.Ember.Materials;
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
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralWallComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmberProceduralWallComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<EmberProceduralWallComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<EmberProceduralWallComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<EmberProceduralWallComponent> ent, ref DamageChangedEvent args)
    {
        PublishDamage(ent, args.Damageable);
    }

    /// <summary>
    /// The threshold a wall is measured against lives in DestructibleComponent, which the client never sees, so
    /// how battered it looks has to be published rather than worked out twice.
    /// </summary>
    private void PublishDamage(EntityUid uid, DamageableComponent? damageable = null)
    {
        if (!Resolve(uid, ref damageable, false))
            return;

        // Walls carry no Appearance of their own, and adding one to every wall prototype would be a line each
        // that a new procedural wall could forget. Ensuring it here means the overlay cannot silently go missing.
        var appearance = EnsureComp<AppearanceComponent>(uid);

        var threshold = GetDestructionThreshold(uid);
        var fraction = threshold > 0
            ? Math.Clamp((float) damageable.TotalDamage / threshold, 0f, 1f)
            : 0f;

        _appearance.SetData(uid, EmberWallVisuals.DamageFraction, fraction, appearance);
    }

    private int GetDestructionThreshold(EntityUid uid)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return 0;

        var highest = 0;
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && trigger.Damage > highest)
                highest = trigger.Damage;
        }

        return highest;
    }

    /// <summary>
    /// Bay's hardness floor. It is checked in <c>can_damage_health</c>, against the raw hit and before any
    /// resistance touches it, so a weak blow is ignored outright rather than merely reduced.
    /// </summary>
    private void OnBeforeDamage(Entity<EmberProceduralWallComponent> ent, ref BeforeDamageChangedEvent args)
    {
        var total = (float) args.Damage.GetTotal();

        // Repairs come through here as negative damage, and Bay only gates damage_health, not restore_health.
        if (total <= 0f)
            return;

        if (TryGetStats(ent.Comp, out var stats) && total < stats.MinimumDamage)
            args.Cancelled = true;
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

        EmberMaterialRadiation.Apply(EntityManager, ent, stats.Radioactivity);

        PublishDamage(ent);
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
