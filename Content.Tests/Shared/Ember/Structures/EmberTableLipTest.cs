using System;
using Content.Shared.Ember.Structures;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Ember.Structures;

/// <summary>
/// A flipped table blocks along one edge of its own tile, and which edge has to agree with the sprite drawn for
/// that direction — the sprite for a given compass point puts the table on that side of the tile.
/// </summary>
[TestFixture]
public sealed class EmberTableLipTest
{
    private static readonly Box2 Southern = new(-0.5f, -0.5f, 0.5f, -0.28125f);

    [Test]
    public void TheLipLiesAlongTheEdgeItFaces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberProceduralTableVisuals.LipFor(Southern, Direction.South).Center.Y,
                Is.LessThan(-0.25f), "South should block the bottom edge.");
            Assert.That(EmberProceduralTableVisuals.LipFor(Southern, Direction.North).Center.Y,
                Is.GreaterThan(0.25f), "North should block the top edge.");
            Assert.That(EmberProceduralTableVisuals.LipFor(Southern, Direction.East).Center.X,
                Is.GreaterThan(0.25f), "East should block the right edge.");
            Assert.That(EmberProceduralTableVisuals.LipFor(Southern, Direction.West).Center.X,
                Is.LessThan(-0.25f), "West should block the left edge.");
        });
    }

    /// <summary>Turning the lip round must not change how much of the tile it takes up.</summary>
    [Test]
    public void TurningItRoundKeepsItTheSameStrip()
    {
        var depth = Southern.Height;

        foreach (var facing in new[] { Direction.South, Direction.North, Direction.East, Direction.West })
        {
            var lip = EmberProceduralTableVisuals.LipFor(Southern, facing);
            var thin = MathF.Min(lip.Width, lip.Height);
            var along = MathF.Max(lip.Width, lip.Height);

            Assert.That(thin, Is.EqualTo(depth).Within(0.001f), $"{facing} is a different depth.");
            Assert.That(along, Is.EqualTo(Southern.Width).Within(0.001f), $"{facing} is a different length.");
        }
    }
}
