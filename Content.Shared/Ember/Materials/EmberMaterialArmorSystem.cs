using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Events;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;

namespace Content.Shared.Ember.Materials;

public sealed class EmberMaterialArmorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralStructureComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<EmberProceduralStructureComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    /// <summary>
    /// The hardness floor. For a pane it is Bay's: <c>health_min_damage = round(material.hardness * 1.25 / 10)</c>, plus
    /// five eighths of the reinforcement's hardness before the division.
    /// </summary>
    /// <remarks>
    /// Walls have had this since the walls were ported; panes never did, and it is the whole reason a window
    /// popped the moment anything warm came near it. Bay checks it against the raw hit and before any
    /// resistance, so a weak blow is ignored outright rather than reduced — which is what makes a borosilicate
    /// pane worth the trouble instead of merely slower to break. Only panes: Bay gives its grilles and its wall
    /// frames no floor at all, and neither do we.
    /// </remarks>
    private void OnBeforeDamage(Entity<EmberProceduralStructureComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Role == EmberProceduralStructureRole.Grille)
            return;

        var total = (float) args.Damage.GetTotal();

        // Repairs arrive as negative damage, and Bay only gates damage_health, never restore_health.
        if (total <= 0f || !TryGetPhysical(ent.Comp.Material, out var material))
            return;

        float floor;

        if (ent.Comp.Role == EmberProceduralStructureRole.WallFrame)
        {
            // Bay gives its wall frames no floor at all, which is the one place we are not following it: a low
            // wall is made of a material like everything else, and a plasteel one that any warm draught can
            // chip is not a low wall anyone would build. It takes the wall's figure, because that is what it is.
            floor = material.Hardness * 2.6f;
        }
        else
        {
            floor = material.Hardness * 1.25f;

            if (TryComp<EmberMaterialReinforcementComponent>(ent, out var reinforcement) &&
                _prototypeManager.TryIndex(reinforcement.Material, out EmberMaterialPrototype? lattice))
            {
                floor += MathF.Round(lattice.Hardness * 0.625f);
            }
        }

        // DM's single-argument round floors, and this one is compared against as an integer.
        if (total < MathF.Floor(floor / 10f))
            args.Cancelled = true;
    }

    private bool TryGetPhysical(
        ProtoId<EmberWallMaterialPrototype> id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EmberMaterialPrototype? material)
    {
        material = null;

        return _prototypeManager.TryIndex(id, out EmberWallMaterialPrototype? wall) &&
               wall.PhysicalMaterial is { } physical &&
               _prototypeManager.TryIndex(physical, out material);
    }

    private void OnDamageModify(Entity<EmberProceduralStructureComponent> ent, ref DamageModifyEvent args)
    {
        if (!_prototypeManager.TryIndex<EmberWallMaterialPrototype>(ent.Comp.Material, out var wallMaterial) ||
            wallMaterial.PhysicalMaterial == null ||
            !_prototypeManager.TryIndex<EmberMaterialPrototype>(wallMaterial.PhysicalMaterial.Value, out var material))
            return;

        if (args.Damage.DamageDict.TryGetValue("Blunt", out var bluntDamage) && bluntDamage > 0)
        {
            var newDamage = FixedPoint2.Max(0, bluntDamage - material.BruteArmor);
            args.Damage.DamageDict["Blunt"] = newDamage;
        }

        if (args.Damage.DamageDict.TryGetValue("Slash", out var slashDamage) && slashDamage > 0)
        {
            var newDamage = FixedPoint2.Max(0, slashDamage - material.BruteArmor);
            args.Damage.DamageDict["Slash"] = newDamage;
        }

        if (args.Damage.DamageDict.TryGetValue("Piercing", out var piercingDamage) && piercingDamage > 0)
        {
            var newDamage = FixedPoint2.Max(0, piercingDamage - material.BruteArmor);
            args.Damage.DamageDict["Piercing"] = newDamage;
        }

        if (args.Damage.DamageDict.TryGetValue("Heat", out var heatDamage) && heatDamage > 0)
        {
            var newDamage = FixedPoint2.Max(0, heatDamage - material.BurnArmor);
            args.Damage.DamageDict["Heat"] = newDamage;
        }

        if (args.Damage.DamageDict.TryGetValue("Shock", out var shockDamage) && shockDamage > 0)
        {
            var newDamage = FixedPoint2.Max(0, shockDamage - material.BurnArmor);
            args.Damage.DamageDict["Shock"] = newDamage;
        }
    }
}
