using System.Text.RegularExpressions;
using Content.Shared.Ember.Paint;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Paint;

[TestFixture]
public sealed class EmberPaintExamineTest
{
    [Test]
    public void TestColorNameMapping()
    {
        // Red
        var redKey = EmberPaintExamineSystem.GetColorNameKey(Color.FromHex("#FF0000"));
        Assert.That(redKey, Is.EqualTo("color-name-red"));

        // Blue
        var blueKey = EmberPaintExamineSystem.GetColorNameKey(Color.FromHex("#0000FF"));
        Assert.That(blueKey, Is.EqualTo("color-name-blue"));

        // Green
        var greenKey = EmberPaintExamineSystem.GetColorNameKey(Color.FromHex("#00FF00"));
        Assert.That(greenKey, Is.EqualTo("color-name-green"));

        // White
        var whiteKey = EmberPaintExamineSystem.GetColorNameKey(Color.FromHex("#FFFFFF"));
        Assert.That(whiteKey, Is.EqualTo("color-name-white"));

        // Black
        var blackKey = EmberPaintExamineSystem.GetColorNameKey(Color.FromHex("#050505"));
        Assert.That(blackKey, Is.EqualTo("color-name-black"));
    }

    /// <summary>
    /// Any paint a player can pick has to end up readable on the tooltip. Saturated blues are the case an HSL
    /// lightness floor misses: lightness 0.6 puts them at a contrast ratio near 2, which is a dark smear.
    /// </summary>
    [Test]
    [TestCase("#330000")] // very dark red
    [TestCase("#0000FF")] // pure blue
    [TestCase("#0335FC")] // the spray painter's blue
    [TestCase("#000080")] // navy
    [TestCase("#2E0854")] // dark purple
    [TestCase("#333333")] // the spray painter's black
    [TestCase("#000000")]
    [TestCase("#FFFFFF")]
    public void ReadableDisplayColorClearsTheContrastThreshold(string hex)
    {
        var readable = EmberPaintExamineSystem.GetReadableDisplayColor(Color.FromHex(hex));

        Assert.That(
            EmberPaintExamineSystem.ContrastOnTooltip(readable),
            Is.GreaterThanOrEqualTo(EmberPaintExamineSystem.MinimumContrast));
    }

    /// <summary>
    /// The word is meant to look like the paint, so a colour that is already readable must come back untouched
    /// rather than being washed towards white.
    /// </summary>
    [Test]
    [TestCase("#03FCD3")] // cyan
    [TestCase("#3AB334")] // green
    [TestCase("#B3A234")] // yellow
    [TestCase("#FF69B4")] // pink
    public void AlreadyReadableColorsAreLeftAlone(string hex)
    {
        var color = Color.FromHex(hex);

        Assert.That(EmberPaintExamineSystem.GetReadableDisplayColor(color), Is.EqualTo(color));
    }

    /// <summary>
    /// The colour tag swallows a value it cannot parse and silently substitutes its own default, so a malformed
    /// hex looks exactly like a working one until you notice every word is the same shade.
    /// </summary>
    [Test]
    [TestCase("#0335FC")]
    [TestCase("#FFFFFF")]
    [TestCase("#000000")]
    [TestCase("#8A2BE2")]
    public void MarkupCarriesAColorTheTagCanParse(string hex)
    {
        var color = Color.FromHex(hex);
        var markup = EmberPaintExamineSystem.MarkupFor(color, "colour");

        var match = Regex.Match(markup, @"^\[color=(?<value>[^\]]+)\]colour\[/color\]$");
        Assert.That(match.Success, Is.True, $"Unexpected markup: {markup}");

        var parsed = Color.TryFromHex(match.Groups["value"].Value);
        Assert.That(parsed, Is.Not.Null, $"Colour tag cannot parse '{match.Groups["value"].Value}'.");

        Assert.That(
            parsed!.Value.ToHexNoAlpha(),
            Is.EqualTo(EmberPaintExamineSystem.GetReadableDisplayColor(color).ToHexNoAlpha()));
    }

    /// <summary>
    /// Lifting lightness has to converge for every hue, including the ones that need the most of it.
    /// </summary>
    [Test]
    public void HueAndSaturationSurviveTheLift()
    {
        var navy = Color.FromHex("#000080");
        var readable = EmberPaintExamineSystem.GetReadableDisplayColor(navy);

        Assert.That(Color.ToHsl(readable).X, Is.EqualTo(Color.ToHsl(navy).X).Within(0.01f));
    }
}
