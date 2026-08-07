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

    /// <summary>
    /// Steel's brute and burn armour. Armour is expressed relative to it so a steel wall behaves exactly as it
    /// did before any of this existed and only other materials move, the same way integrity is anchored.
    /// </summary>
    public const float ReferenceArmor = 7f;

    /// <summary>Bay steps the damage overlay through sixteen levels of opacity.</summary>
    public const int DamageOverlaySteps = 16;

    /// <summary>
    /// Bay: <c>damage_overlays[round(percent / 100 * 16) + 1]</c>, each step a sixteenth more opaque than the
    /// last. Returns how opaque the overlay should be, or zero when the wall is unmarked.
    /// </summary>
    public static float GetDamageOverlayAlpha(float damageFraction)
    {
        if (damageFraction <= 0f)
            return 0f;

        var step = Math.Clamp(
            (int) MathF.Round(damageFraction * DamageOverlaySteps) + 1,
            1,
            DamageOverlaySteps);

        return (step * (256f / DamageOverlaySteps) - 1f) / 255f;
    }

    public static EmberWallStats For(EmberMaterialPrototype material, EmberMaterialPrototype? reinforcement)
    {
        var integrity = material.Integrity * 1.5f;

        // Below this a hit does nothing at all, which is what stops a crowbar from chipping away at a diamond
        // wall given enough patience. Bay checks it against the raw damage, before armour is applied.
        var minimumDamage = material.Hardness * 2.6f;

        // Bay scales both armour values by 0.4 before inverting them. Expressed relative to steel that factor
        // cancels out, so the sums stay raw here.
        var brute = (float) material.BruteArmor;
        var burn = (float) material.BurnArmor;
        var radioactivity = material.Radioactivity ?? 0f;

        if (reinforcement != null)
        {
            integrity += MathF.Round(reinforcement.Integrity * 0.75f);
            minimumDamage += MathF.Round(reinforcement.Hardness * 1.9f);
            brute += reinforcement.BruteArmor;
            burn += reinforcement.BurnArmor;
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
    /// Materials carry armour as a divisor while the damage pipeline wants a multiplier, so Bay inverts it. The
    /// result is then expressed relative to steel.
    /// </summary>
    /// <remarks>
    /// Bay's raw <c>1 / (armour * 0.4)</c> would stack on top of the resistances SS14 walls already carry in
    /// their damage modifier set, and a steel bulkhead would end up absorbing a rifle round almost entirely.
    /// Anchoring to steel keeps the ratios between materials exactly as Bay has them — wood still takes seven
    /// times what steel does — while leaving the balance of a plain steel wall where the rest of the game
    /// expects it.
    /// </remarks>
    private static float AsCoefficient(float armour)
    {
        // Bay would make an unarmoured wall immune here, dividing by zero and landing on a resistance of zero.
        // No ported material has that, and the sensible reading of "no armour" is the weakest wall, not the
        // strongest, so it is clamped to what a single point of armour would give.
        return ReferenceArmor / MathF.Max(armour, 1f);
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
