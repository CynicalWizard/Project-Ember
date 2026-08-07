using Content.Shared.Ember.Doors;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Doors;

[TestFixture]
[TestOf(typeof(EmberAirlockPaintStyle))]
public sealed class EmberAirlockPaintStyleTest
{
    [TestCase("basic", "EmberAirlockBasic", "Airlock")]
    [TestCase("engineering", "EmberAirlockEngineering", "AirlockEngineering")]
    [TestCase("Engineering", "EmberAirlockEngineering", "AirlockEngineering")]
    [TestCase("cargo", "EmberAirlockLogistics", "AirlockCargo")]
    [TestCase("science", "EmberAirlockEpistemics", "AirlockScience")]
    [TestCase("external", "EmberAirlockExternal", "AirlockExternal")]
    // A docking port is its own style rather than a shuttle-shaped repaint of the basic one, so it is the
    // amber collar that answers here.
    [TestCase("docking", "EmberAirlockDocking", "AirlockShuttle")]
    public void KnownSprayPainterStylesMapToEmberProceduralStylesAndPreviewEntities(
        string sprayStyle,
        string expectedEmberStyle,
        string expectedPreview)
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberAirlockPaintStyle.TryGetStyle(sprayStyle, out var emberStyle), Is.True);
            Assert.That(emberStyle, Is.EqualTo(expectedEmberStyle));
            Assert.That(EmberAirlockPaintStyle.TryGetPreviewPrototype(sprayStyle, out var preview), Is.True);
            Assert.That(preview, Is.EqualTo(expectedPreview));
        });
    }

    [Test]
    public void UnknownStyleReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberAirlockPaintStyle.TryGetStyle("not-a-style", out _), Is.False);
            Assert.That(EmberAirlockPaintStyle.TryGetPreviewPrototype("not-a-style", out _), Is.False);
        });
    }
}
