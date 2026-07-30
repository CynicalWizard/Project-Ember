using System.Numerics;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.Examine;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Paint;

public sealed class EmberPaintExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralWallComponent, ExaminedEvent>(OnWallExamined);
        SubscribeLocalEvent<EmberProceduralStructureComponent, ExaminedEvent>(OnStructureExamined);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, ExaminedEvent>(OnAirlockExamined);
    }

    private void OnWallExamined(EntityUid uid, EmberProceduralWallComponent component, ExaminedEvent args)
    {
        if (component.PaintColor is { } paintColor)
        {
            var formattedColor = FormatColor(paintColor);
            args.PushMarkup(Loc.GetString("ember-wall-examined-paint", ("color", formattedColor)));
        }

        if (component.StripeColor is { } stripeColor)
        {
            var formattedStripe = FormatColor(stripeColor);
            args.PushMarkup(Loc.GetString("ember-wall-examined-stripe", ("color", formattedStripe)));
        }
    }

    private void OnStructureExamined(EntityUid uid, EmberProceduralStructureComponent component, ExaminedEvent args)
    {
        if (component.Color is { } color)
        {
            var formattedColor = FormatColor(color);
            args.PushMarkup(Loc.GetString("ember-structure-examined-paint", ("color", formattedColor)));
        }
    }

    private void OnAirlockExamined(EntityUid uid, EmberProceduralAirlockComponent component, ExaminedEvent args)
    {
        if (component.DoorColor is { } doorColor)
        {
            var formatted = FormatColor(doorColor);
            args.PushMarkup(Loc.GetString("ember-airlock-examined-door", ("color", formatted)));
        }

        if (component.StripeColor is { } stripeColor)
        {
            var formatted = FormatColor(stripeColor);
            args.PushMarkup(Loc.GetString("ember-airlock-examined-stripe", ("color", formatted)));
        }

        if (component.WindowColor is { } windowColor)
        {
            var formatted = FormatColor(windowColor);
            args.PushMarkup(Loc.GetString("ember-airlock-examined-window", ("color", formatted)));
        }
    }

    public static string FormatColor(Color color)
    {
        var key = GetColorNameKey(color);
        var name = Robust.Shared.Localization.Loc.GetString(key);
        var displayColor = GetReadableDisplayColor(color);
        return $"[color=#{displayColor.ToHexNoAlpha()}]{name}[/color]";
    }

    public static Color GetReadableDisplayColor(Color color)
    {
        var hsl = Color.ToHsl(color);
        // Ensure text is readable on dark UI backgrounds by enforcing lightness >= 0.60
        var lightness = Math.Max(hsl.Z, 0.60f);
        return Color.FromHsl(new Vector4(hsl.X, hsl.Y, lightness, 1f));
    }

    public static string GetColorNameKey(Color color)
    {
        var hsl = Color.ToHsl(color);
        var h = hsl.X * 360f;
        var s = hsl.Y;
        var l = hsl.Z;

        if (l < 0.15f)
            return "color-name-black";

        if (l > 0.88f && s < 0.20f)
            return "color-name-white";

        if (s < 0.15f)
        {
            if (l < 0.40f)
                return "color-name-dark-gray";
            if (l > 0.70f)
                return "color-name-light-gray";
            return "color-name-gray";
        }

        if (h >= 15f && h <= 45f && l < 0.40f && s < 0.70f)
            return "color-name-brown";

        if (h < 15f || h >= 345f)
            return l < 0.45f ? "color-name-dark-red" : "color-name-red";

        if (h >= 15f && h < 45f)
            return l < 0.45f ? "color-name-dark-orange" : "color-name-orange";

        if (h >= 45f && h < 70f)
            return l < 0.45f ? "color-name-dark-yellow" : "color-name-yellow";

        if (h >= 70f && h < 165f)
            return l < 0.45f ? "color-name-dark-green" : (l > 0.75f ? "color-name-lime" : "color-name-green");

        if (h >= 165f && h < 195f)
            return l < 0.45f ? "color-name-dark-cyan" : "color-name-cyan";

        if (h >= 195f && h < 255f)
            return l < 0.45f ? "color-name-dark-blue" : "color-name-blue";

        if (h >= 255f && h < 290f)
            return l < 0.45f ? "color-name-dark-purple" : "color-name-purple";

        return l < 0.45f ? "color-name-dark-pink" : "color-name-pink";
    }
}
