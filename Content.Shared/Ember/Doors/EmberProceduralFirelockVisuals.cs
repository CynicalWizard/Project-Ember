using Robust.Shared.Maths;

namespace Content.Shared.Ember.Doors;

public static class EmberProceduralFirelockVisuals
{
    /// <summary>
    /// Bay's <c>firedoor/on_update_icon</c> orientation rule: a shutter whose only blending neighbours are north
    /// and/or south faces east, everything else faces south.
    /// </summary>
    /// <param name="vertical">Something the shutter blends with sits to the north or south.</param>
    /// <param name="horizontal">Something the shutter blends with sits to the east or west.</param>
    public static Direction FacingFor(bool vertical, bool horizontal)
    {
        return vertical && !horizontal ? Direction.East : Direction.South;
    }
}
