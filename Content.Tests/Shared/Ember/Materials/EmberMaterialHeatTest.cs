using Content.Shared.Ember.Materials;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Materials;

/// <summary>
/// Bay's fire damage curve, which is shallow enough that the rounding rule decides most of it.
/// </summary>
[TestFixture]
[TestOf(typeof(EmberMaterialHeat))]
public sealed class EmberMaterialHeatTest
{
    [Test]
    public void AFireBelowTheMeltingPointDoesNothing()
    {
        Assert.That(EmberMaterialHeat.Damage(1000f, 1800f), Is.Zero);
        Assert.That(EmberMaterialHeat.Damage(1800f, 1800f), Is.Zero, "Exactly at the point is not over it.");
    }

    [Test]
    public void AnythingOverThePointHurtsAtLeastOnce()
    {
        Assert.That(EmberMaterialHeat.Damage(1801f, 1800f), Is.EqualTo(1f));
        Assert.That(EmberMaterialHeat.Damage(1899f, 1800f), Is.EqualTo(1f));
    }

    /// <summary>
    /// Rounding down rather than to nearest. Half of every hundred-kelvin band lands here, and at the low end
    /// where most fires sit that is the difference between one point of damage and two.
    /// </summary>
    [Test]
    public void PartOfABandIsNotABand()
    {
        Assert.That(EmberMaterialHeat.Damage(2050f, 1800f), Is.EqualTo(2f));
        Assert.That(EmberMaterialHeat.Damage(2100f, 1800f), Is.EqualTo(3f));
    }

    /// <summary>Glass melts at 373K, so an ordinary fire tears through a window and leaves plasteel alone.</summary>
    [Test]
    public void TheMaterialIsWhatDecides()
    {
        Assert.That(EmberMaterialHeat.Damage(1273f, 373f), Is.EqualTo(9f));
        Assert.That(EmberMaterialHeat.Damage(1273f, 6000f), Is.Zero);
    }
}
