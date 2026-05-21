using Content.Shared.Ember.Walls;
using Content.Shared.SprayPainter;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.SprayPainter;

[TestFixture]
[TestOf(typeof(SprayPainterWallPaint))]
public sealed class SprayPainterWallPaintTest
{
    [Test]
    public void PaintWallSetsPaintColor()
    {
        var wall = new EmberProceduralWallComponent();
        var color = Color.FromHex("#224466");

        SprayPainterWallPaint.Apply(wall, SprayPainterWallMode.PaintWall, color);

        Assert.That(wall.PaintColor, Is.EqualTo(color));
    }

    [Test]
    public void ClearWallPaintRestoresMaterialColor()
    {
        var wall = new EmberProceduralWallComponent
        {
            PaintColor = Color.Red,
        };

        SprayPainterWallPaint.Apply(wall, SprayPainterWallMode.ClearWallPaint, null);

        Assert.That(wall.PaintColor, Is.Null);
    }

    [Test]
    public void PaintStripeSetsStripeColor()
    {
        var wall = new EmberProceduralWallComponent();
        var color = Color.FromHex("#DDAA33");

        SprayPainterWallPaint.Apply(wall, SprayPainterWallMode.PaintStripe, color);

        Assert.That(wall.StripeColor, Is.EqualTo(color));
    }

    [Test]
    public void ClearStripeRestoresBaseStripeColor()
    {
        var wall = new EmberProceduralWallComponent
        {
            StripeColor = Color.Blue,
        };

        SprayPainterWallPaint.Apply(wall, SprayPainterWallMode.ClearStripe, null);

        Assert.That(wall.StripeColor, Is.Null);
    }
}
