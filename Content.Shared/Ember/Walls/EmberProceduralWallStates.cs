namespace Content.Shared.Ember.Walls;

public static class EmberProceduralWallStates
{
    public const string Blank = "blank";
    public const string StripeBase = "stripe";

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
}
