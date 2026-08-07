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
    
    /// <summary>
    /// How hot a fire counts as, for a curve that was written for fires a great deal cooler than ours.
    /// </summary>
    /// <remarks>
    /// Below the knee nothing happens: Bay's numbers pass through untouched, because at Bay's temperatures
    /// Bay's curve is right. Above it the temperature keeps climbing, but logarithmically, so a fire ten times
    /// past the knee counts as three and a bit rather than ten. That matters in both directions. A hard ceiling
    /// made anything above it fireproof, and nothing should survive a million kelvin -- that is hotter than the
    /// inside of a star. A straight line made a tritium fire worth hundreds of points a second against hull
    /// that is supposed to be the answer to fire.
    ///
    /// EMBER-TODO: this is a stopgap and worth saying so. Temperature alone cannot tell a candle from a star,
    /// and the measurements behind the default -- see EmberFireTemperatureTest -- say a tritium flame settles
    /// at the same seventy thousand kelvin whether it is five moles or two canisters, because the temperature
    /// follows the ratio of fuel to oxidiser rather than the amount. A model where scale does not matter is a
    /// model that will keep needing fudge factors. Gases and combustion want rewriting, and the damage formula
    /// with them, so that it works from the energy a fire actually delivers to a surface. That is its own PR.
    /// </remarks>
    public static float Effective(float temperature, float knee)
    {
        if (knee <= 0f || temperature <= knee)
            return temperature;

        return knee + knee * MathF.Log(1f + (temperature - knee) / knee);
    }

    public static float Damage(float temperature, float meltingPoint)
    {
        if (temperature <= meltingPoint)
            return 0f;

        return MathF.Max(MathF.Floor((temperature - meltingPoint) / KelvinPerDamage), 1f);
    }
}
