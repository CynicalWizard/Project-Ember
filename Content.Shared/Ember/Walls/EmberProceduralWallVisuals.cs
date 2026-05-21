namespace Content.Shared.Ember.Walls;

public readonly record struct EmberProceduralWallLayerVisuals(
    string StateBase,
    Color BaseColor,
    Color? PaintColor,
    Color? StripeColor,
    string? ReinforcementStateBase,
    Color? ReinforcementColor,
    string SmoothKey);

public static class EmberProceduralWallVisuals
{
    public static EmberProceduralWallLayerVisuals Resolve(
        EmberProceduralWallComponent wall,
        EmberWallMaterialPrototype material)
    {
        var stateBase = material.StateBase;
        var baseColor = wall.PaintColor ?? material.Color;
        var paintColor = wall.PaintColor;
        var reinforcementStateBase = wall.Reinforced ? material.ReinforcementStateBase : null;
        Color? reinforcementColor = reinforcementStateBase != null
            ? wall.PaintColor ?? material.ReinforcementColor ?? material.Color
            : null;
        var smoothKey = material.SmoothKey ?? stateBase;

        return new EmberProceduralWallLayerVisuals(
            stateBase,
            baseColor,
            paintColor,
            wall.StripeColor,
            reinforcementStateBase,
            reinforcementColor,
            smoothKey);
    }
}
