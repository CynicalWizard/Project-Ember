using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.Damage;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Structures;

/// <summary>
/// Gives a table the toughness of whatever it is plated with, following Bay's <c>update_material</c>. A glass
/// table is a glass table wherever it stands, rather than whatever number happened to be typed into its
/// prototype, and reinforcing one is worth exactly what the second material is worth.
/// </summary>
/// <remarks>
/// The prototype still says how a table comes apart — which sound it makes, what it leaves behind — and how its
/// thresholds sit relative to one another, since a table that creaks before it breaks should keep doing that.
/// Only the size of them is taken away from it: the largest is set from the material and the rest keep their
/// share of it.
/// </remarks>
public sealed class EmberProceduralTableDamageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralTableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmberProceduralTableComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<EmberProceduralTableComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(Entity<EmberProceduralTableComponent> ent, ref MapInitEvent args)
    {
        // A table's materials are fixed by its prototype — plating or reinforcing one swaps the entity rather
        // than editing it — so this runs once and the thresholds below never compound.
        ApplyHealth(ent, GetStats(ent.Comp).Health);
    }

    private void ApplyHealth(EntityUid uid, float health)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        var highest = 0;
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && trigger.Damage > highest)
                highest = trigger.Damage;
        }

        if (highest <= 0)
            return;

        var scale = EmberTableMaterialStats.ThresholdFor(health) / highest;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger)
                trigger.Damage = Math.Max(1, (int) MathF.Round(trigger.Damage * scale));
        }
    }

    /// <summary>
    /// Bay's hardness floor, checked against the raw hit before any resistance touches it, so a weak blow is
    /// ignored outright rather than merely reduced.
    /// </summary>
    private void OnBeforeDamage(Entity<EmberProceduralTableComponent> ent, ref BeforeDamageChangedEvent args)
    {
        var total = (float) args.Damage.GetTotal();

        // Repairs arrive as negative damage, and Bay only gates damage_health, not restore_health.
        if (total <= 0f)
            return;

        if (total < GetStats(ent.Comp).MinimumDamage)
            args.Cancelled = true;
    }

    private void OnDamageModify(Entity<EmberProceduralTableComponent> ent, ref DamageModifyEvent args)
    {
        var multiplier = GetStats(ent.Comp).DamageMultiplier;

        if (multiplier != 1f)
            args.Damage *= multiplier;
    }

    private EmberTableStats GetStats(EmberProceduralTableComponent component)
    {
        return EmberTableMaterialStats.For(
            Resolve(component.Material),
            Resolve(component.Reinforcement));
    }

    private EmberMaterialPrototype? Resolve(ProtoId<EmberMaterialPrototype>? id)
    {
        return id is { } material && _prototype.TryIndex(material, out EmberMaterialPrototype? prototype)
            ? prototype
            : null;
    }
}
