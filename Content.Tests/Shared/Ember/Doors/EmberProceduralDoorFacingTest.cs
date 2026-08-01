using Content.Shared.Ember.Doors;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Doors;

[TestFixture]
[TestOf(typeof(EmberProceduralDoorFacing))]
public sealed class EmberProceduralDoorFacingTest
{
    [Test]
    public void DoorInAVerticalWallRunFacesEast()
    {
        Assert.That(
            EmberProceduralDoorFacing.FacingFor(vertical: true, horizontal: false),
            Is.EqualTo(Direction.East));
    }

    [Test]
    public void DoorInAHorizontalWallRunFacesSouth()
    {
        Assert.That(
            EmberProceduralDoorFacing.FacingFor(vertical: false, horizontal: true),
            Is.EqualTo(Direction.South));
    }

    [Test]
    public void DoorAtAJunctionFacesSouth()
    {
        Assert.That(
            EmberProceduralDoorFacing.FacingFor(vertical: true, horizontal: true),
            Is.EqualTo(Direction.South));
    }

    [Test]
    public void FreestandingDoorFacesSouth()
    {
        Assert.That(
            EmberProceduralDoorFacing.FacingFor(vertical: false, horizontal: false),
            Is.EqualTo(Direction.South));
    }
}
