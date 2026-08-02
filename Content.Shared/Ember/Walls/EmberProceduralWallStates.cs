namespace Content.Shared.Ember.Walls;

public static class EmberProceduralWallStates
{
    public const string Blank = "blank";
    public const string StripeBase = "stripe";

    /// <summary>Bay's <c>overlay_damage</c>, one mask covering the whole tile rather than a corner.</summary>
    public const string DamageOverlay = "overlay_damage";

    public static string Base(string stateBase, int corner)
    {
        return $"{stateBase}{corner}";
    }

    public static string Paint(string stateBase, int corner, bool visible)
    {
        return visible ? $"{stateBase}_paint{corner}" : Blank;
    }

    public static string Stripe(int corner, bool visible)
    {
        return visible ? $"{StripeBase}{corner}" : Blank;
    }

    public static string Reinforcement(string? stateBase, int corner)
    {
        return stateBase != null ? $"{stateBase}{corner}" : Blank;
    }

    /// <summary>
    /// The seam Bay draws along joins with a different material, built from its own connection set.
    /// </summary>
    public static string Other(string stateBase, int corner, bool visible)
    {
        return visible ? $"{stateBase}_other{corner}" : Blank;
    }
}
