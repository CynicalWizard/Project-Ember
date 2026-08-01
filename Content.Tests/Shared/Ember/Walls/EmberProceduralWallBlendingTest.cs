using System.Collections.Generic;
using Content.Shared.Ember.Walls;
using NUnit.Framework;
using Robust.Shared.Maths;

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
    public void IdenticalUnpaintedWallsJoinSeamlessly()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, null, "solid", null),
            Is.EqualTo(EmberWallJoin.Seamless));
    }

    [Test]
    public void IdenticalWallsPaintedDifferentlyGetASeam()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, Color.Red, "solid", Color.Blue),
            Is.EqualTo(EmberWallJoin.Edge));
    }

    [Test]
    public void IdenticalWallsPaintedTheSameStaySeamless()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, Color.Red, "solid", Color.Red),
            Is.EqualTo(EmberWallJoin.Seamless));
    }

    [Test]
    public void ListedMaterialJoinsWithASeam()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, null, "wood", null),
            Is.EqualTo(EmberWallJoin.Edge));
    }

    [Test]
    public void UnlistedMaterialDoesNotJoin()
    {
        Assert.That(
            EmberProceduralWallBlending.Classify("solid", SteelBlends, null, "metal", null),
            Is.EqualTo(EmberWallJoin.None));
    }

    [Test]
    public void BlendingIsDirectional()
    {
        // Steel lists stone, but a plain metal wall lists nothing, so the join is only seen from the steel side.
        Assert.Multiple(() =>
        {
            Assert.That(
                EmberProceduralWallBlending.Classify("solid", SteelBlends, null, "stone", null),
                Is.EqualTo(EmberWallJoin.Edge));
            Assert.That(
                EmberProceduralWallBlending.Classify("stone", NoBlends, null, "solid", null),
                Is.EqualTo(EmberWallJoin.None));
        });
    }

    [Test]
    public void SelfReferencingBlendEntryDoesNotSplitAUniformRun()
    {
        var selfListing = new Dictionary<string, bool> { ["stone"] = true };

        Assert.That(
            EmberProceduralWallBlending.Classify("stone", selfListing, null, "stone", null),
            Is.EqualTo(EmberWallJoin.Seamless));
    }

    [Test]
    public void DisabledBlendEntryDoesNotJoin()
    {
        var disabled = new Dictionary<string, bool> { ["wood"] = false };

        Assert.That(
            EmberProceduralWallBlending.Classify("solid", disabled, null, "wood", null),
            Is.EqualTo(EmberWallJoin.None));
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
