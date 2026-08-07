using Content.Shared.Ember.Materials;
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

    /// <summary>
    /// Bay's materials say what may be painted on them. Sandstone has nowhere to put a hazard stripe, so the
    /// sprayer refuses rather than drawing one that the sprite sheet has no state for.
    /// </summary>
    [Test]
    public void MaterialFlagsDecideWhichModesAreOffered()
    {
        var stripeable = Material(main: true, stripe: true);
        var plain = Material(main: true, stripe: false);
        var unpaintable = Material(main: false, stripe: false);

        Assert.Multiple(() =>
        {
            Assert.That(SprayPainterWallPaint.CanApply(stripeable, SprayPainterWallMode.PaintStripe), Is.True);
            Assert.That(SprayPainterWallPaint.CanApply(plain, SprayPainterWallMode.PaintStripe), Is.False);
            Assert.That(SprayPainterWallPaint.CanApply(plain, SprayPainterWallMode.ClearStripe), Is.False);
            Assert.That(SprayPainterWallPaint.CanApply(plain, SprayPainterWallMode.PaintWall), Is.True);
            Assert.That(SprayPainterWallPaint.CanApply(unpaintable, SprayPainterWallMode.PaintWall), Is.False);
        });
    }

    /// <summary>
    /// The glass wall materials have no physical material to carry flags, and locking them out of paint entirely
    /// would be a worse guess than letting them through.
    /// </summary>
    [Test]
    public void AMaterialWithNoFlagsToConsultStaysPaintable()
    {
        Assert.That(SprayPainterWallPaint.CanApply(null, SprayPainterWallMode.PaintWall), Is.True);
    }

#pragma warning disable RA0039 // Inert data; nothing is looked up by id.
    private static EmberMaterialPrototype Material(bool main, bool stripe)
    {
        return new EmberMaterialPrototype
        {
            Key = "test",
            WallPaintableMain = main,
            WallPaintableStripe = stripe,
        };
    }
#pragma warning restore RA0039
}
