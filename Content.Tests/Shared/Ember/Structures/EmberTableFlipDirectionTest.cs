using System.Numerics;
using Content.Shared.Ember.Structures;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Structures;

/// <summary>
/// A table goes over away from whoever pushed it. Getting that backwards is not obvious from the code, because
/// Robust measures entity rotation from south while the maths angle of a vector is measured from east, and the
/// two are only ninety degrees apart — enough to look like a bug in the straight-row check rather than in the
/// direction itself.
/// </summary>
[TestFixture]
public sealed class EmberTableFlipDirectionTest
{
    private static readonly Vector2 Table = new(10f, 10f);

    [Test]
    [TestCase(0f, 1f, Direction.South)]
    [TestCase(0f, -1f, Direction.North)]
    [TestCase(1f, 0f, Direction.West)]
    [TestCase(-1f, 0f, Direction.East)]
    public void ATableGoesOverAwayFromWhoeverPushedIt(float offsetX, float offsetY, Direction expected)
    {
        var user = Table + new Vector2(offsetX, offsetY);

        Assert.That(EmberProceduralTableVisuals.FlipDirection(user, Table), Is.EqualTo(expected));
    }

    /// <summary>
    /// Standing off to one side still has to give a compass point, or the table lies across its own tile and
    /// reports the row it belongs to as being in the way.
    /// </summary>
    [Test]
    [TestCase(0.2f, 1f)]
    [TestCase(-0.4f, 1f)]
    [TestCase(0.9f, 1f)]
    public void StandingOffToOneSideStillPushesItStraight(float offsetX, float offsetY)
    {
        var user = Table + new Vector2(offsetX, offsetY);

        Assert.That(EmberProceduralTableVisuals.FlipDirection(user, Table), Is.EqualTo(Direction.South));
    }
}
