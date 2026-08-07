using Content.Shared.Ember.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Doors;

[TestFixture]
[TestOf(typeof(EmberProceduralAirlockVisuals))]
public sealed class EmberProceduralAirlockVisualsTest
{
    [Test]
    public void DoorColorCanResolveFromDepartmentPrototype()
    {
        var style = new EmberAirlockStylePrototype
        {
            ID = "Engineering",
            DoorDepartment = "Engineering",
            StripeColor = Color.FromHex("#FF0000"),
        };

        var department = new DepartmentPrototype
        {
            Color = Color.FromHex("#EFB341"),
        };

        var component = new EmberProceduralAirlockComponent
        {
            Style = "Engineering",
        };

        var visuals = EmberProceduralAirlockVisuals.Resolve(component, style, department, null);

        Assert.Multiple(() =>
        {
            Assert.That(visuals.DoorColor, Is.EqualTo(Color.FromHex("#EFB341")));
            Assert.That(visuals.Fill, Is.EqualTo(EmberAirlockFill.Color));
            Assert.That(visuals.FillColor, Is.EqualTo(Color.FromHex("#EFB341")));
            Assert.That(visuals.StripeColor, Is.EqualTo(Color.FromHex("#FF0000")));
        });
    }

    [Test]
    public void GlassAirlockUsesGlassFillAndSkipsStripeFill()
    {
        var style = new EmberAirlockStylePrototype
        {
            ID = "Medical",
            DoorColor = Color.White,
            StripeDepartment = "Medical",
        };

        var department = new DepartmentPrototype
        {
            Color = Color.FromHex("#52B4E9"),
        };

        var component = new EmberProceduralAirlockComponent
        {
            Style = "Medical",
            Glass = true,
        };

        var visuals = EmberProceduralAirlockVisuals.Resolve(component, style, null, department);

        Assert.Multiple(() =>
        {
            Assert.That(visuals.DoorColor, Is.EqualTo(Color.White));
            Assert.That(visuals.Fill, Is.EqualTo(EmberAirlockFill.Glass));
            Assert.That(visuals.FillColor, Is.EqualTo(Color.White));
            Assert.That(visuals.StripeColor, Is.EqualTo(Color.FromHex("#52B4E9")));
            Assert.That(visuals.ShowStripeFill, Is.False);
        });
    }

    [Test]
    public void ClosedDoorStateResolvesToStaticClosedSpriteState()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberProceduralAirlockVisuals.SpriteStateFor(DoorState.Opening), Is.EqualTo("opening"));
            Assert.That(EmberProceduralAirlockVisuals.SpriteStateFor(DoorState.Closing), Is.EqualTo("closing"));
            Assert.That(EmberProceduralAirlockVisuals.SpriteStateFor(DoorState.Open), Is.EqualTo("open"));
            Assert.That(EmberProceduralAirlockVisuals.SpriteStateFor(DoorState.Closed), Is.EqualTo("closed"));
            Assert.That(EmberProceduralAirlockVisuals.IsTransitionState(DoorState.Opening), Is.True);
            Assert.That(EmberProceduralAirlockVisuals.IsTransitionState(DoorState.Closing), Is.True);
            Assert.That(EmberProceduralAirlockVisuals.IsTransitionState(DoorState.Open), Is.False);
            Assert.That(EmberProceduralAirlockVisuals.IsTransitionState(DoorState.Closed), Is.False);
        });
    }
}
