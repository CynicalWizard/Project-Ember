namespace Content.Shared.Ember.Walls;

/// <summary>
/// How strongly a wall joins to one of its neighbours, mirroring the return values of Bay's
/// <c>/turf/simulated/wall/can_join_with</c>.
/// </summary>
public enum EmberWallJoin : byte
{
    /// <summary>Nothing to join to; the wall draws its own outer border here.</summary>
    None = 0,

    /// <summary>The two read as one continuous surface, with no seam drawn between them.</summary>
    Seamless = 1,

    /// <summary>
    /// They join, but a seam is drawn along the join. Bay feeds these into a second connection set that the
    /// <c>_other</c> overlay is built from.
    /// </summary>
    Edge = 2,
}

public static class EmberProceduralWallBlending
{
    /// <summary>
    /// Bay's <c>can_join_with</c>: a material listed in the other wall's blend table joins with a seam, an
    /// identical material joins seamlessly unless the two are painted differently, and anything else does not
    /// join at all.
    /// </summary>
    public static EmberWallJoin Classify(
        string selfKey,
        IReadOnlyDictionary<string, bool> selfBlendKeys,
        Color? selfPaint,
        string otherKey,
        Color? otherPaint)
    {
        // A material listing its own key is a data mistake; treat it as the identical-material case rather than
        // drawing a seam down the middle of a uniform wall run.
        if (selfKey != otherKey && selfBlendKeys.TryGetValue(otherKey, out var blends) && blends)
            return EmberWallJoin.Edge;

        if (selfKey != otherKey)
            return EmberWallJoin.None;

        return selfPaint == otherPaint ? EmberWallJoin.Seamless : EmberWallJoin.Edge;
    }

    /// <summary>
    /// Bay treats low wall frames as a full blend and everything else on its blend list — doors, grilles,
    /// windows — as a seamed one. Windoors are on the no-blend list and never join.
    /// </summary>
    public static EmberWallJoin ClassifyStructure(EmberStructureBlend blend)
    {
        return blend switch
        {
            EmberStructureBlend.Full => EmberWallJoin.Seamless,
            EmberStructureBlend.Edge => EmberWallJoin.Edge,
            _ => EmberWallJoin.None,
        };
    }
}

public enum EmberStructureBlend : byte
{
    None = 0,
    Full = 1,
    Edge = 2,
}
