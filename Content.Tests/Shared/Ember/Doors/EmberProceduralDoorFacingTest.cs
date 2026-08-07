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

    [Test]
    public void AnAlreadyCorrectDoorIsNotOffset()
    {
        Assert.That(
            EmberProceduralDoorFacing.OffsetFor(Direction.South, Direction.South),
            Is.EqualTo(EmberDoorDirOffset.None));
    }

    /// <summary>
    /// The offsets have to match the renderer's clockwise cycle (north to east to south to west), because it
    /// applies them on top of the direction it derived from the entity and eye rotation.
    /// </summary>
    [Test]
    [TestCase(Direction.South, Direction.West, EmberDoorDirOffset.Clockwise)]
    [TestCase(Direction.West, Direction.North, EmberDoorDirOffset.Clockwise)]
    [TestCase(Direction.North, Direction.East, EmberDoorDirOffset.Clockwise)]
    [TestCase(Direction.East, Direction.South, EmberDoorDirOffset.Clockwise)]
    [TestCase(Direction.South, Direction.East, EmberDoorDirOffset.CounterClockwise)]
    [TestCase(Direction.East, Direction.North, EmberDoorDirOffset.CounterClockwise)]
    [TestCase(Direction.South, Direction.North, EmberDoorDirOffset.Flip)]
    [TestCase(Direction.East, Direction.West, EmberDoorDirOffset.Flip)]
    public void OffsetFollowsTheRenderersClockwiseCycle(Direction from, Direction to, EmberDoorDirOffset expected)
    {
        Assert.That(EmberProceduralDoorFacing.OffsetFor(from, to), Is.EqualTo(expected));
    }

    /// <summary>
    /// A door a mapper left rotated must still end up drawn at the facing its walls call for.
    /// </summary>
    [Test]
    [TestCase(Direction.South)]
    [TestCase(Direction.East)]
    [TestCase(Direction.North)]
    [TestCase(Direction.West)]
    public void OffsetCancelsWhateverRotationTheDoorWasPlacedAt(Direction placed)
    {
        var facing = EmberProceduralDoorFacing.FacingFor(vertical: true, horizontal: false);
        var offset = EmberProceduralDoorFacing.OffsetFor(placed, facing);

        Assert.That(Apply(placed, offset), Is.EqualTo(facing));
    }

    /// <summary>
    /// Mirrors the client's <c>OffsetRsiDir</c>, which is what the offset is fed into.
    /// </summary>
    private static Direction Apply(Direction dir, EmberDoorDirOffset offset)
    {
        var result = dir;

        for (var i = 0; i < (int) offset; i++)
        {
            result = result switch
            {
                Direction.North => Direction.East,
                Direction.East => Direction.South,
                Direction.South => Direction.West,
                _ => Direction.North,
            };
        }

        return result;
    }
}
