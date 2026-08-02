using System.Collections.Generic;
using Content.Shared.Ember.Walls;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Walls;

[TestFixture]
[TestOf(typeof(EmberProceduralWallBlending))]
public sealed class EmberProceduralWallBlendingTest
{
    private static readonly Dictionary<string, bool> SteelBlends = new()
    {
        ["wood"] = true,
        ["stone"] = true,
    };

    private static readonly Dictionary<string, bool> NoBlends = new();

    [Test]
    public void IdenticalWallsJoinSeamlessly()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, "solid"),
            Is.EqualTo(EmberWallJoin.Seamless));
    }

    [Test]
    public void ListedMaterialJoinsWithASeam()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, "wood"),
            Is.EqualTo(EmberWallJoin.Edge));
    }

    [Test]
    public void UnlistedMaterialDoesNotJoin()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, "metal"),
            Is.EqualTo(EmberWallJoin.None));
    }

    [Test]
    public void BlendingIsDirectional()
    {
        // Steel lists stone, but a plain metal wall lists nothing, so the join is only seen from the steel side.
        Assert.Multiple(() =>
        {
            Assert.That(
                EmberProceduralWallBlending.Classify("solid", SteelBlends, "stone"),
                Is.EqualTo(EmberWallJoin.Edge));
            Assert.That(
                EmberProceduralWallBlending.Classify("stone", NoBlends, "solid"),
                Is.EqualTo(EmberWallJoin.None));
        });
    }

    [Test]
    public void SelfReferencingBlendEntryDoesNotSplitAUniformRun()
    {
        var selfListing = new Dictionary<string, bool> { ["stone"] = true };

        Assert.That(
            EmberProceduralWallBlending.Classify("stone", selfListing, "stone"),
            Is.EqualTo(EmberWallJoin.Seamless));
    }

    [Test]
    public void DisabledBlendEntryDoesNotJoin()
    {
        var disabled = new Dictionary<string, bool> { ["wood"] = false };

        Assert.That(
            EmberProceduralWallBlending.Classify("solid", disabled, "wood"),
            Is.EqualTo(EmberWallJoin.None));
    }

    /// <summary>
    /// A tile can hold more than one thing a wall reacts to, and the strongest offer has to win so the result
    /// does not depend on which anchored entity is enumerated first.
    /// </summary>
    [Test]
    public void SeamlessOutranksASeamWhichOutranksNoJoin()
    {
        Assert.That(EmberWallJoin.Seamless, Is.GreaterThan(EmberWallJoin.Edge));
        Assert.That(EmberWallJoin.Edge, Is.GreaterThan(EmberWallJoin.None));
    }

    [Test]
    public void LowWallFramesFullyBlendWhileEverythingElseGetsASeam()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                EmberProceduralWallBlending.ClassifyStructure(EmberStructureBlend.Full),
                Is.EqualTo(EmberWallJoin.Seamless));
            Assert.That(
                EmberProceduralWallBlending.ClassifyStructure(EmberStructureBlend.Edge),
                Is.EqualTo(EmberWallJoin.Edge));
            Assert.That(
                EmberProceduralWallBlending.ClassifyStructure(EmberStructureBlend.None),
                Is.EqualTo(EmberWallJoin.None));
        });
    }
}
