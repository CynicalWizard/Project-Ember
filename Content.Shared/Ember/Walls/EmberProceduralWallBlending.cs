namespace Content.Shared.Ember.Walls;

/// <summary>
/// How strongly a wall joins to one of its neighbours, mirroring the return values of Bay's
/// <c>/turf/simulated/wall/can_join_with</c>. Ordered weakest to strongest so the best offer on a tile can be
/// picked with a plain comparison.
/// </summary>
public enum EmberWallJoin : byte
{
    /// <summary>Nothing to join to; the wall draws its own outer border here.</summary>
    None = 0,

    /// <summary>
    /// They join, but a seam is drawn along the join. Bay feeds these into a second connection set that the
    /// <c>_other</c> overlay is built from.
    /// </summary>
    Edge = 1,

    /// <summary>The two read as one continuous surface, with no seam drawn between them.</summary>
    Seamless = 2,
}

public static class EmberProceduralWallBlending
{
    /// <summary>
    /// Bay's <c>can_join_with</c>: a material listed in the other wall's blend table joins with a seam, an
    /// identical material joins seamlessly, and anything else does not join at all.
    /// </summary>
    /// <remarks>
    /// Bay also seams two walls of the same material whenever their paint differs. That reads badly here: the
    /// <c>_other</c> mask is a bevelled rim, so a single wall painted inside an otherwise untouched run gets
    /// outlined on all four sides and stops looking like part of the run at all. Paint is already obvious from
    /// the colour, so it does not decide joins; the seam now means one thing only, which is that two different
    /// materials meet.
    /// </remarks>
    public static EmberWallJoin Classify(
        string selfKey,
        IReadOnlyDictionary<string, bool> selfBlendKeys,
        string otherKey)
    {
        // A material listing its own key is a data mistake; treat it as the identical-material case rather than
        // drawing a seam down the middle of a uniform wall run.
        if (selfKey == otherKey)
            return EmberWallJoin.Seamless;

        return selfBlendKeys.TryGetValue(otherKey, out var blends) && blends
            ? EmberWallJoin.Edge
            : EmberWallJoin.None;
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
