using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
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
