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
    /// <summary>The examine panel texture, /Textures/Interface/Nano/tooltip.png.</summary>
    private static readonly Color TooltipPanel = Color.FromHex("#1B1B1C");

    /// <summary>
    /// What ExamineSystem puts in the popup's <c>ModulateSelfOverride</c>. That only covers the panel's own
    /// draw — children get <c>Modulate</c>, which the popup leaves alone — so it darkens the background behind
    /// the text without touching the text itself.
    /// </summary>
    private static readonly Color TooltipModulate = Color.LightGray;

    private const float TooltipAlpha = 0.90f;

    /// <summary>
    /// Stand-in for whatever tile the popup happens to sit over. The panel only lets a tenth of it through, so
    /// a mid grey is close enough for either extreme.
    /// </summary>
    private static readonly Color WorldBehindTooltip = Color.Gray;

    /// <summary>WCAG AA for body text.</summary>
    public const float MinimumContrast = 4.5f;

    private const float LightnessStep = 0.02f;

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

        return MarkupFor(color, name);
    }

    /// <summary>
    /// Wraps <paramref name="name"/> in the colour it describes.
    /// </summary>
    /// <remarks>
    /// ToHexNoAlpha already returns the leading '#', so writing one as well produces "##RRGGBB", which the colour
    /// tag cannot parse. It does not fail loudly: ColorTag falls back to its default, and that default is built
    /// from int literals that overflow the byte conversion, so every word came out the same dark grey no matter
    /// what the paint was.
    /// </remarks>
    public static string MarkupFor(Color color, string name)
    {
        return $"[color={GetReadableDisplayColor(color).ToHexNoAlpha()}]{name}[/color]";
    }

    /// <summary>
    /// The examine tooltip sits on a near-black panel, so a paint colour is only usable as text if it stands out
    /// against that panel.
    /// </summary>
    /// <remarks>
    /// A flat floor on HSL lightness is not enough, because lightness is not perceptual: a saturated blue sits at
    /// lightness 0.5 and still reads as a dark smear, while a yellow at the same lightness is already glaring.
    /// Lifting until the contrast ratio clears the threshold instead leaves colours that were readable to begin
    /// with completely untouched, so the word keeps looking like the paint it describes.
    /// </remarks>
    public static Color GetReadableDisplayColor(Color color)
    {
        if (ContrastOnTooltip(color) >= MinimumContrast)
            return color;

        var hsl = Color.ToHsl(color);
        var lightness = hsl.Z;
        Color result;

        // Raising lightness washes the colour towards white, so this always terminates.
        do
        {
            lightness = Math.Min(lightness + LightnessStep, 1f);
            result = Color.FromHsl(new Vector4(hsl.X, hsl.Y, lightness, 1f));
        }
        while (lightness < 1f && ContrastOnTooltip(result) < MinimumContrast);

        return result;
    }

    /// <summary>
    /// Contrast of tooltip text in <paramref name="text"/> against the panel behind it, measured on the pixels
    /// the player ends up looking at rather than on the value handed to the markup.
    /// </summary>
    /// <remarks>
    /// The popup is neither opaque nor drawn at full brightness, so the panel texture on its own is not what
    /// ends up behind the text.
    /// </remarks>
    public static float ContrastOnTooltip(Color text)
    {
        var panel = Color.InterpolateBetween(
            WorldBehindTooltip,
            TooltipPanel * TooltipModulate,
            TooltipAlpha);

        return ContrastRatio(text, panel);
    }

    public static float ContrastRatio(Color a, Color b)
    {
        var first = RelativeLuminance(a);
        var second = RelativeLuminance(b);
        var (lighter, darker) = first > second ? (first, second) : (second, first);

        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(Color color)
    {
        return 0.2126f * Linearise(color.R) + 0.7152f * Linearise(color.G) + 0.0722f * Linearise(color.B);
    }

    private static float Linearise(float channel)
    {
        return channel <= 0.03928f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
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
