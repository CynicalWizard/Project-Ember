using Content.Shared.Ember.Doors;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Doors;

[TestFixture]
[TestOf(typeof(EmberMaterialDoorVisuals))]
public sealed class EmberMaterialDoorVisualsTest
{
    /// <summary>
    /// Bay builds all four states by suffixing the icon base, which is the whole reason one sheet covers every
    /// material.
    /// </summary>
    [Test]
    public void StatesAreTheIconBasePlusASuffix()
    {
        var states = EmberMaterialDoorVisuals.StatesFor("wood");

        Assert.Multiple(() =>
        {
            Assert.That(states.Closed, Is.EqualTo("wood"));
            Assert.That(states.Open, Is.EqualTo("woodopen"));
            Assert.That(states.Opening, Is.EqualTo("woodopening"));
            Assert.That(states.Closing, Is.EqualTo("woodclosing"));
        });
    }

    /// <summary>
    /// Bay's own material data points at a "cult" icon base that material_doors.dmi never had. Falling back
    /// keeps that from becoming an error texture on the door.
    /// </summary>
    [Test]
    public void AnUnknownIconBaseFallsBackInsteadOfProducingMissingStates()
    {
        Assert.That(EmberMaterialDoorVisuals.Resolve("cult"),
            Is.EqualTo(EmberMaterialDoorVisuals.FallbackBase));
    }

    [Test]
    [TestCase("metal")]
    [TestCase("stone")]
    [TestCase("wood")]
    [TestCase("plastic")]
    [TestCase("resin")]
    public void KnownIconBasesAreKept(string iconBase)
    {
        Assert.That(EmberMaterialDoorVisuals.Resolve(iconBase), Is.EqualTo(iconBase));
    }
}
