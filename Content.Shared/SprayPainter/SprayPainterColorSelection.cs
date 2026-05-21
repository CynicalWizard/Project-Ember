using Content.Shared.SprayPainter.Components;

namespace Content.Shared.SprayPainter;

public static class SprayPainterColorSelection
{
    public static bool TryGetPickedColor(SprayPainterComponent painter, out Color color)
    {
        if (painter.PickedCustomColor)
        {
            color = painter.CustomColor.WithAlpha(1f);
            return true;
        }

        color = default;
        return painter.PickedColor is { } key &&
            painter.ColorPalette.TryGetValue(key, out color);
    }
}
