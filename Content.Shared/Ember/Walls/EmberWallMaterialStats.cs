using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Walls;

/// <summary>
/// What a wall's materials make of it, as Bay's <c>calculate_damage_data</c> works it out.
/// </summary>
public readonly record struct EmberWallStats(
    float Integrity,
    float MinimumDamage,
    float BruteCoefficient,
    float BurnCoefficient,
    float ExplosionCoefficient,
    float Radioactivity);

public static class EmberWallMaterialStats
{
    /// <summary>
    /// A steel bulkhead in Bay: integrity 150 becomes 225 health. SS14's own walls sit at 300, so this is the
    /// point where the two scales are pinned together and steel keeps the number the rest of the game expects.
    /// </summary>
    public const float ReferenceIntegrity = 225f;

    /// <summary>
    /// Bay's radioactivity figures are on its own scale — 12 for uranium, 20 for supermatter — and it already
    /// divides them down when handing them to its radiation system, by 15 for fuel assemblies. SS14 intensities
    /// live in a much narrower band where a singularity is 2, so a tenth puts a uranium bulkhead at 1.2: worth
    /// keeping away from, not instantly lethal.
    /// </summary>
    public const float RadiationIntensityScale = 0.1f;

    public static EmberWallStats For(EmberMaterialPrototype material, EmberMaterialPrototype? reinforcement)
    {
        var integrity = material.Integrity * 1.5f;

        // Below this a hit does nothing at all, which is what stops a crowbar from chipping away at a diamond
        // wall given enough patience. Bay checks it against the raw damage, before armour is applied.
        var minimumDamage = material.Hardness * 2.6f;

        var brute = material.BruteArmor * 0.4f;
        var burn = material.BurnArmor * 0.4f;
        var radioactivity = material.Radioactivity ?? 0f;

        if (reinforcement != null)
        {
            integrity += MathF.Round(reinforcement.Integrity * 0.75f);
            minimumDamage += MathF.Round(reinforcement.Hardness * 1.9f);
            brute += reinforcement.BruteArmor * 0.4f;
            burn += reinforcement.BurnArmor * 0.4f;
            radioactivity += (reinforcement.Radioactivity ?? 0f) / 2f;
        }

        return new EmberWallStats(
            integrity,
            MathF.Round(minimumDamage / 10f),
            AsCoefficient(brute),
            AsCoefficient(burn),
            GetExplosionCoefficient(material, reinforcement),
            radioactivity);
    }

    /// <summary>
    /// Materials carry armour as a divisor while the damage pipeline wants a multiplier, so Bay inverts it here.
    /// </summary>
    private static float AsCoefficient(float armour)
    {
        return armour > 0f ? MathF.Round(1f / armour, 2) : 1f;
    }

    /// <summary>
    /// Bay's <c>5 / explosion_resistance</c>, with steel's 5 as the break-even point, and the better of the
    /// wall's two materials winning rather than the two adding up.
    /// </summary>
    private static float GetExplosionCoefficient(
        EmberMaterialPrototype material,
        EmberMaterialPrototype? reinforcement)
    {
        var resistance = material.ExplosionResistance;

        if (reinforcement != null && reinforcement.ExplosionResistance > resistance)
            resistance = reinforcement.ExplosionResistance;

        return resistance > 0f ? 5f / resistance : 1f;
    }
}
