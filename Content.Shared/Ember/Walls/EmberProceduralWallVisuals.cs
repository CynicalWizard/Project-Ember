using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Walls;

public readonly record struct EmberProceduralWallLayerVisuals(
    string StateBase,
    Color BaseColor,
    Color? PaintColor,
    Color? StripeColor,
    string? ReinforcementStateBase,
    Color? ReinforcementColor,
    string SmoothKey,
    IReadOnlyDictionary<string, bool> BlendKeys,
    bool HasEdges)
{
    /// <summary>
    /// Bay colours the <c>_other</c> seam overlay with the stripe colour when there is one, and with the wall's
    /// own colour otherwise.
    /// </summary>
    public Color EdgeColor => StripeColor ?? BaseColor;
}

public static class EmberProceduralWallVisuals
{
    private static readonly Dictionary<string, bool> NoBlending = new();

    public static EmberProceduralWallLayerVisuals Resolve(
        EmberProceduralWallComponent wall,
        EmberWallMaterialPrototype material,
        EmberMaterialPrototype? physical = null)
    {
        var stateBase = material.StateBase;
        var baseColor = wall.PaintColor ?? material.Color;

        var paintColor = wall.PaintColor;
        var reinforcementStateBase = wall.Reinforced ? material.ReinforcementStateBase : null;
        Color? reinforcementColor = reinforcementStateBase != null
            ? wall.PaintColor ?? material.ReinforcementColor ?? material.Color
            : null;
        var smoothKey = material.SmoothKey ?? stateBase;

        // The blend table and the edge flag live on the physical material, which is where the Bay data was
        // ported to. Wall materials with no physical counterpart (the glass ones) can state them directly.
        var blendKeys = material.BlendKeys ?? physical?.WallBlendIcons ?? NoBlending;
        var hasEdges = material.HasEdges ?? physical?.WallHasEdges ?? false;

        return new EmberProceduralWallLayerVisuals(
            stateBase,
            baseColor,
            paintColor,
            wall.StripeColor,
            reinforcementStateBase,
            reinforcementColor,
            smoothKey,
            blendKeys,
            hasEdges);
    }
}
