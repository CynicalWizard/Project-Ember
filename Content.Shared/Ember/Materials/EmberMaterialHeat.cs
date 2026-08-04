namespace Content.Shared.Ember.Materials;

/// <summary>
/// How hard a fire hurts something made of a given material.
/// </summary>
public static class EmberMaterialHeat
{
    /// <summary>Bay hands out one point of damage per this many kelvin above the melting point.</summary>
    public const float KelvinPerDamage = 100f;

    /// <summary>
    /// Bay's <c>fire_act</c>: a point of damage for every hundred kelvin the fire runs above the melting point,
    /// and never less than one once it is over it at all.
    /// </summary>
    /// <remarks>
    /// The rounding is downward because that is what BYOND's single-argument <c>round</c> does. Rounding to
    /// nearest would hand out an extra point across half of every hundred-kelvin band, which is a fifty per
    /// cent difference at the low end where most fires actually sit.
    /// </remarks>
    public static float Damage(float temperature, float meltingPoint)
    {
        if (temperature <= meltingPoint)
            return 0f;

        return MathF.Max(MathF.Floor((temperature - meltingPoint) / KelvinPerDamage), 1f);
    }
}
