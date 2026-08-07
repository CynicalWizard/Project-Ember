using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;

namespace Content.Shared.SprayPainter;

public static class SprayPainterWallPaint
{
    public static bool RequiresColor(SprayPainterWallMode mode)
    {
        return mode is SprayPainterWallMode.PaintWall or SprayPainterWallMode.PaintStripe;
    }

    /// <summary>
    /// Bay's paint sprayer checks the wall material's paintable flags before it does anything: a stripe only goes
    /// on materials that have somewhere to put one, and some materials do not take paint at all.
    /// </summary>
    /// <remarks>
    /// A wall material with no physical counterpart, which is the glass ones, has no flags to consult and is
    /// left paintable rather than silently locked out.
    /// </remarks>
    public static bool CanApply(EmberMaterialPrototype? material, SprayPainterWallMode mode)
    {
        if (material == null)
            return true;

        return mode switch
        {
            SprayPainterWallMode.PaintWall or SprayPainterWallMode.ClearWallPaint => material.WallPaintableMain,
            SprayPainterWallMode.PaintStripe or SprayPainterWallMode.ClearStripe => material.WallPaintableStripe,
            _ => false,
        };
    }

    public static void Apply(EmberProceduralWallComponent wall, SprayPainterWallMode mode, Color? color)
    {
        switch (mode)
        {
            case SprayPainterWallMode.PaintWall:
                wall.PaintColor = color;
                break;
            case SprayPainterWallMode.ClearWallPaint:
                wall.PaintColor = null;
                break;
            case SprayPainterWallMode.PaintStripe:
                wall.StripeColor = color;
                break;
            case SprayPainterWallMode.ClearStripe:
                wall.StripeColor = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
}

public enum SprayPainterWallMode : byte
{
    PaintWall,
    ClearWallPaint,
    PaintStripe,
    ClearStripe,
}
