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

    [Test]
    public void TestReadableDisplayColorBoostsDarkLightness()
    {
        var darkRed = Color.FromHex("#330000"); // Very dark red
        var readable = EmberPaintExamineSystem.GetReadableDisplayColor(darkRed);
        var hsl = Color.ToHsl(readable);

        Assert.That(hsl.Z, Is.GreaterThanOrEqualTo(0.55f));
    }
}
