using Content.Shared.Ember.Structures;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Structures;

[TestFixture]
[TestOf(typeof(EmberProceduralStructureCorners))]
public sealed class EmberProceduralStructureCornersTest
{
    [Test]
    public void EastFacingSpriteLayersMatchIconSmoothCornerOrder()
    {
        var corners = EmberProceduralStructureCorners.MapToLayers(
            Direction.East,
            se: "SE",
            ne: "NE",
            nw: "NW",
            sw: "SW");

        Assert.Multiple(() =>
        {
            Assert.That(corners.SE, Is.EqualTo("NE"));
            Assert.That(corners.NE, Is.EqualTo("NW"));
            Assert.That(corners.NW, Is.EqualTo("SW"));
            Assert.That(corners.SW, Is.EqualTo("SE"));
        });
    }

    [Test]
    public void NorthFacingSpriteLayersMatchIconSmoothCornerOrder()
    {
        var corners = EmberProceduralStructureCorners.MapToLayers(
            Direction.North,
            se: "SE",
            ne: "NE",
            nw: "NW",
            sw: "SW");

        Assert.Multiple(() =>
        {
            Assert.That(corners.SE, Is.EqualTo("NW"));
            Assert.That(corners.NE, Is.EqualTo("SW"));
            Assert.That(corners.NW, Is.EqualTo("SE"));
            Assert.That(corners.SW, Is.EqualTo("NE"));
        });
    }
}
