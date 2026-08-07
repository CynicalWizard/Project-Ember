using Robust.Shared.Maths;

namespace Content.Shared.Ember.Doors;

/// <summary>
/// Which neighbouring structures a door smooths against. Bay keeps a separate <c>blend_objects</c> list per door
/// type: the base door blends with low walls, windows and grilles, while firedoors swap grilles out for other
/// firedoors. Walls are always included.
/// </summary>
[Flags]
public enum EmberDoorBlendTargets : byte
{
    None = 0,
    WallFrames = 1 << 0,
    Windows = 1 << 1,
    Grilles = 1 << 2,
    Firelocks = 1 << 3,

    /// <summary><c>/obj/machinery/door/blend_objects</c>.</summary>
    Airlock = WallFrames | Windows | Grilles,

    /// <summary><c>/obj/machinery/door/firedoor/blend_objects</c>.</summary>
    Firelock = WallFrames | Windows | Firelocks,
}

/// <summary>
/// Quarter turns to add to a sprite's direction, mirroring the client's <c>DirectionOffset</c>.
/// </summary>
public enum EmberDoorDirOffset : byte
{
    None = 0,
    Clockwise = 1,
    Flip = 2,
    CounterClockwise = 3,
}

public static class EmberProceduralDoorFacing
{
    /// <summary>
    /// Bay's shared <c>on_update_icon</c> orientation rule for airlocks and firedoors: a door whose only
    /// blending neighbours are north and/or south faces east, everything else faces south.
    /// </summary>
    /// <param name="vertical">Something the door blends with sits to the north or south.</param>
    /// <param name="horizontal">Something the door blends with sits to the east or west.</param>
    public static Direction FacingFor(bool vertical, bool horizontal)
    {
        return vertical && !horizontal ? Direction.East : Direction.South;
    }

    /// <summary>
    /// The offset that turns a door drawn at <paramref name="from"/> into one drawn at <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// This is deliberately an offset rather than an outright direction override. The renderer picks a layer's
    /// direction from the entity's rotation plus the eye rotation and only then applies the offset, so an offset
    /// keeps working when the player spins the camera, where an override would pin the door to one frame and
    /// leave it skewed against the wall it sits in.
    /// </remarks>
    public static EmberDoorDirOffset OffsetFor(Direction from, Direction to)
    {
        var steps = (to.GetClockwiseIndex() - from.GetClockwiseIndex() + 4) % 4;
        return (EmberDoorDirOffset) steps;
    }

    private static int GetClockwiseIndex(this Direction direction)
    {
        return direction switch
        {
            Direction.North => 0,
            Direction.East => 1,
            Direction.South => 2,
            Direction.West => 3,
            _ => 2,
        };
    }
}
