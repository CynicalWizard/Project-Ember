using Content.Shared.Ember.Walls;

namespace Content.Shared.SprayPainter;

public static class SprayPainterWallPaint
{
    public static bool RequiresColor(SprayPainterWallMode mode)
    {
        return mode is SprayPainterWallMode.PaintWall or SprayPainterWallMode.PaintStripe;
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
