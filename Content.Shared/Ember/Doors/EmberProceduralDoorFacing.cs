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
}
