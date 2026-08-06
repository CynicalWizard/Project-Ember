using Content.Shared.Ember.Doors;

namespace Content.Shared.SprayPainter;

public static class SprayPainterAirlockPaint
{
    public static bool RequiresColor(SprayPainterAirlockMode mode)
    {
        return mode is SprayPainterAirlockMode.PaintDoor
            or SprayPainterAirlockMode.PaintStripe
            or SprayPainterAirlockMode.PaintWindow
            or SprayPainterAirlockMode.PaintDocking;
    }

    public static void Apply(EmberProceduralAirlockComponent airlock, SprayPainterAirlockMode mode, Color? color)
    {
        switch (mode)
        {
            case SprayPainterAirlockMode.PaintDoor:
                airlock.DoorColor = color;
                break;
            case SprayPainterAirlockMode.ClearDoor:
                airlock.DoorColor = null;
                break;
            case SprayPainterAirlockMode.PaintStripe:
                airlock.StripeColor = color;
                break;
            case SprayPainterAirlockMode.ClearStripe:
                airlock.StripeColor = null;
                break;
            case SprayPainterAirlockMode.PaintWindow:
                airlock.WindowColor = color;
                break;
            case SprayPainterAirlockMode.ClearWindow:
                airlock.WindowColor = null;
                break;
            case SprayPainterAirlockMode.PaintDocking:
                airlock.DockingColor = color;
                break;
            case SprayPainterAirlockMode.ClearDocking:
                airlock.DockingColor = null;
                break;
        }
    }
}

public enum SprayPainterAirlockMode : byte
{
    ApplyStyle,
    PaintDoor,
    ClearDoor,
    PaintStripe,
    ClearStripe,
    PaintWindow,
    ClearWindow,
    PaintDocking,
    ClearDocking,
}
