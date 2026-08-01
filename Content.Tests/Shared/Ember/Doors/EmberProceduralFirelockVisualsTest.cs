using Content.Shared.Ember.Doors;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Doors;

[TestFixture]
[TestOf(typeof(EmberProceduralFirelockVisuals))]
public sealed class EmberProceduralFirelockVisualsTest
{
    [Test]
    public void ShutterInAVerticalWallRunFacesEast()
    {
        Assert.That(
            EmberProceduralFirelockVisuals.FacingFor(vertical: true, horizontal: false),
            Is.EqualTo(Direction.East));
    }

    [Test]
    public void ShutterInAHorizontalWallRunFacesSouth()
    {
        Assert.That(
            EmberProceduralFirelockVisuals.FacingFor(vertical: false, horizontal: true),
            Is.EqualTo(Direction.South));
    }

    [Test]
    public void ShutterAtAJunctionFacesSouth()
    {
        Assert.That(
            EmberProceduralFirelockVisuals.FacingFor(vertical: true, horizontal: true),
            Is.EqualTo(Direction.South));
    }

    [Test]
    public void FreestandingShutterFacesSouth()
    {
        Assert.That(
            EmberProceduralFirelockVisuals.FacingFor(vertical: false, horizontal: false),
            Is.EqualTo(Direction.South));
    }
}
