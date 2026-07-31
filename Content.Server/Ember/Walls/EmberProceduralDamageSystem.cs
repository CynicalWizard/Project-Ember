using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.Damage;
using Content.Shared.Ember.Walls;
using Content.Shared.Ember.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Walls;

public sealed class EmberProceduralDamageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<EmberProceduralWallComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmberProceduralWallComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(EntityUid uid, EmberProceduralWallComponent component, MapInitEvent args)
    {
        RecalculateDamage(uid, component);
    }

    private void RecalculateDamage(EntityUid uid, EmberProceduralWallComponent component)
    {
        if (!_prototypeManager.TryIndex(component.Material, out var wallMaterial))
            return;

        if (wallMaterial.PhysicalMaterial == null || !_prototypeManager.TryIndex(wallMaterial.PhysicalMaterial.Value, out var baseMaterial))
            return;

        EmberMaterialPrototype? reinfMaterial = null;
        if (component.ReinforcementMaterial != null && _prototypeManager.TryIndex(component.ReinforcementMaterial.Value, out var reinfWallMaterial))
        {
            if (reinfWallMaterial.PhysicalMaterial != null)
                _prototypeManager.TryIndex(reinfWallMaterial.PhysicalMaterial.Value, out reinfMaterial);
        }

        // Calculate Integrity
        float baseIntegrity = baseMaterial.Integrity * 1.5f;
        float reinfIntegrity = reinfMaterial != null ? reinfMaterial.Integrity * 0.75f : 0f;
        float totalIntegrity = baseIntegrity + reinfIntegrity;

        // SierraBay Steel (Integrity 150) base wall has 225 calculated integrity.
        float integrityMultiplier = totalIntegrity / 225f;

        if (TryComp<DestructibleComponent>(uid, out var destructible))
        {
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is DamageTrigger damageTrigger)
                {
                    // Scale damage. We use Math.Max to ensure it's at least 1.
                    damageTrigger.Damage = Math.Max(1, (int)(damageTrigger.Damage * integrityMultiplier));
                }
            }
        }
    }

    private void OnDamageModify(EntityUid uid, EmberProceduralWallComponent component, DamageModifyEvent args)
    {
        if (!_prototypeManager.TryIndex(component.Material, out var wallMaterial))
            return;

        if (wallMaterial.PhysicalMaterial == null || !_prototypeManager.TryIndex(wallMaterial.PhysicalMaterial.Value, out var baseMaterial))
            return;

        EmberMaterialPrototype? reinfMaterial = null;
        if (component.ReinforcementMaterial != null && _prototypeManager.TryIndex(component.ReinforcementMaterial.Value, out var reinfWallMaterial))
        {
            if (reinfWallMaterial.PhysicalMaterial != null)
                _prototypeManager.TryIndex(reinfWallMaterial.PhysicalMaterial.Value, out reinfMaterial);
        }

        float baseBrute = baseMaterial.BruteArmor * 0.4f;
        float baseBurn = baseMaterial.BurnArmor * 0.4f;
        
        float reinfBrute = reinfMaterial != null ? reinfMaterial.BruteArmor * 0.4f : 0f;
        float reinfBurn = reinfMaterial != null ? reinfMaterial.BurnArmor * 0.4f : 0f;

        float totalBruteArmor = baseBrute + reinfBrute;
        float totalBurnArmor = baseBurn + reinfBurn;

        // In SierraBay, armor acts as a divisor: damage / (armor > 0 ? armor : 1) or similar.
        // Usually, SS13 armor is a direct multiplier or percentage reduction.
        // Assuming armor > 0 reduces damage (e.g., armor 10 reduces damage by factor of 10? No, SS13 formula is complex).
        // Let's implement a simple SS14 percentage reduction: 
        // 10 armor = 10% reduction. Cap at 90%.
        
        float bruteReduction = Math.Clamp(totalBruteArmor * 0.05f, 0f, 0.9f); // Example: 10 armor = 50% reduction
        float burnReduction = Math.Clamp(totalBurnArmor * 0.05f, 0f, 0.9f);

        // Apply reduction to Brute damage types
        if (args.Damage.DamageDict.TryGetValue("Blunt", out var blunt))
            args.Damage.DamageDict["Blunt"] = blunt * (1f - bruteReduction);
            
        if (args.Damage.DamageDict.TryGetValue("Slash", out var slash))
            args.Damage.DamageDict["Slash"] = slash * (1f - bruteReduction);
            
        if (args.Damage.DamageDict.TryGetValue("Piercing", out var pierce))
            args.Damage.DamageDict["Piercing"] = pierce * (1f - bruteReduction);

        // Apply reduction to Burn damage types
        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat))
            args.Damage.DamageDict["Heat"] = heat * (1f - burnReduction);
            
        if (args.Damage.DamageDict.TryGetValue("Shock", out var shock))
            args.Damage.DamageDict["Shock"] = shock * (1f - burnReduction);
    }
}
