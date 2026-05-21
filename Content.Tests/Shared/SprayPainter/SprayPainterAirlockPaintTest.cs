using Content.Shared.Ember.Doors;
using Content.Shared.SprayPainter;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.SprayPainter;

[TestFixture]
[TestOf(typeof(SprayPainterAirlockPaint))]
public sealed class SprayPainterAirlockPaintTest
{
    [Test]
    public void PaintDoorSetsDoorColor()
    {
        var airlock = new EmberProceduralAirlockComponent();
        var color = Color.FromHex("#336699");

        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.PaintDoor, color);

        Assert.That(airlock.DoorColor, Is.EqualTo(color));
    }

    [Test]
    public void PaintStripeSetsStripeColor()
    {
        var airlock = new EmberProceduralAirlockComponent();
        var color = Color.FromHex("#AA5500");

        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.PaintStripe, color);

        Assert.That(airlock.StripeColor, Is.EqualTo(color));
    }

    [Test]
    public void PaintWindowSetsWindowColor()
    {
        var airlock = new EmberProceduralAirlockComponent();
        var color = Color.FromHex("#55AAFF");

        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.PaintWindow, color);

        Assert.That(airlock.WindowColor, Is.EqualTo(color));
    }

    [Test]
    public void ClearModesRestoreStyleColors()
    {
        var airlock = new EmberProceduralAirlockComponent
        {
            DoorColor = Color.Red,
            StripeColor = Color.Green,
            WindowColor = Color.Blue,
        };

        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.ClearDoor, null);
        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.ClearStripe, null);
        SprayPainterAirlockPaint.Apply(airlock, SprayPainterAirlockMode.ClearWindow, null);

        Assert.Multiple(() =>
        {
            Assert.That(airlock.DoorColor, Is.Null);
            Assert.That(airlock.StripeColor, Is.Null);
            Assert.That(airlock.WindowColor, Is.Null);
        });
    }
}
