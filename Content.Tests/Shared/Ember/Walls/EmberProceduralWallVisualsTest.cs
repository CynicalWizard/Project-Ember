using Content.Shared.Ember.Walls;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Walls;

[TestFixture]
[TestOf(typeof(EmberProceduralWallVisuals))]
public sealed class EmberProceduralWallVisualsTest
{
    [Test]
    public void PaintColorOverridesMaterialBaseColor()
    {
        var material = new EmberWallMaterialPrototype
        {
            ID = "Steel",
            StateBase = "solid",
            Color = Color.FromHex("#696969"),
        };

        var component = new EmberProceduralWallComponent
        {
            Material = "Steel",
            PaintColor = Color.FromHex("#334455"),
            StripeColor = Color.FromHex("#AA5500"),
        };

        var visuals = EmberProceduralWallVisuals.Resolve(component, material);

        Assert.Multiple(() =>
        {
            Assert.That(visuals.StateBase, Is.EqualTo("solid"));
            Assert.That(visuals.BaseColor, Is.EqualTo(Color.FromHex("#334455")));
            Assert.That(visuals.PaintColor, Is.EqualTo(Color.FromHex("#334455")));
            Assert.That(visuals.StripeColor, Is.EqualTo(Color.FromHex("#AA5500")));
            Assert.That(visuals.SmoothKey, Is.EqualTo("solid"));
        });
    }

    [Test]
    public void UnpaintedWallUsesMaterialColorAndPlainSmoothKey()
    {
        var material = new EmberWallMaterialPrototype
        {
            ID = "Wood",
            StateBase = "wood",
            Color = Color.FromHex("#7A4D2A"),
        };

        var component = new EmberProceduralWallComponent
        {
            Material = "Wood",
        };

        var visuals = EmberProceduralWallVisuals.Resolve(component, material);

        Assert.Multiple(() =>
        {
            Assert.That(visuals.StateBase, Is.EqualTo("wood"));
            Assert.That(visuals.BaseColor, Is.EqualTo(Color.FromHex("#7A4D2A")));
            Assert.That(visuals.PaintColor, Is.Null);
            Assert.That(visuals.StripeColor, Is.Null);
            Assert.That(visuals.SmoothKey, Is.EqualTo("wood"));
        });
    }

    [Test]
    public void ReinforcedWallUsesMaterialReinforcementOverlayWithoutChangingSmoothKey()
    {
        var material = new EmberWallMaterialPrototype
        {
            ID = "Steel",
            StateBase = "solid",
            Color = Color.FromHex("#696969"),
            ReinforcementStateBase = "reinf_over",
            ReinforcementColor = Color.FromHex("#696969"),
        };

        var component = new EmberProceduralWallComponent
        {
            Material = "Steel",
            Reinforced = true,
        };

        var visuals = EmberProceduralWallVisuals.Resolve(component, material);

        Assert.Multiple(() =>
        {
            Assert.That(visuals.StateBase, Is.EqualTo("solid"));
            Assert.That(visuals.BaseColor, Is.EqualTo(Color.FromHex("#696969")));
            Assert.That(visuals.ReinforcementStateBase, Is.EqualTo("reinf_over"));
            Assert.That(visuals.ReinforcementColor, Is.EqualTo(Color.FromHex("#696969")));
            Assert.That(visuals.SmoothKey, Is.EqualTo("solid"));
        });
    }
}
