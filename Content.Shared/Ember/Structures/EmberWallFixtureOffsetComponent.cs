namespace Content.Shared.Ember.Structures;

/// <summary>
/// Sinks a fitting into the wall behind it, by an amount that depends on which wall that is.
/// </summary>
/// <remarks>
/// Walls are drawn tall rather than flat, so a fitting on the far wall of a room has to ride up the face of it
/// while one on the near wall sits against a thin edge and needs nothing. Bay does this in
/// <c>/obj/machinery/light/on_update_icon</c>: it looks at the tile its dir points into and, only if that is
/// dense, pushes itself 21 pixels for a wall to the north and 10 for one to either side, leaving a wall to the
/// south alone.
///
/// Its dir is the wall; ours is the way the fitting faces, measured across our own maps, where 6493 of 6716
/// lights have a wall behind them and open floor in front. So the depths here are keyed by the way it faces and
/// the wall is looked for behind it.
///
/// This is only worth having for fittings that stand on the floor beside their wall, which is where our lights
/// are — 8707 of 8711 of them. Anything mounted on the wall tile itself carries its lean in its art instead.
/// </remarks>
[RegisterComponent]
public sealed partial class EmberWallFixtureOffsetComponent : Component
{
    /// <summary>
    /// How far into the wall behind to sink, in pixels, by the direction the fitting faces.
    /// </summary>
    [DataField]
    public Dictionary<Direction, int> Depth = new();

    /// <summary>Whether the tile behind is currently a wall, so the offset is only recomputed on a change.</summary>
    [ViewVariables]
    public bool AgainstWall;

    [ViewVariables]
    public Direction Facing = Direction.Invalid;
}
