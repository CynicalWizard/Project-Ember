using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Structures;

/// <summary>
/// Bay's <c>update_material</c>, pinned. A table is worth exactly what it is plated with, and none of these
/// numbers were chosen here.
/// </summary>
[TestFixture]
[TestOf(typeof(EmberTableMaterialStats))]
public sealed class EmberTableMaterialStatsTest
{
    /// <summary>Steel is what everything else is measured against: integrity 150, so half of it is 75.</summary>
    [Test]
    public void SteelMatchesTheReferenceTable()
    {
        var stats = EmberTableMaterialStats.For(Material(integrity: 150, hardness: 60), null);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Health, Is.EqualTo(EmberTableMaterialStats.ReferenceHealth));
            Assert.That(stats.MinimumDamage, Is.EqualTo(6f)); // round(60 / 10)
            Assert.That(stats.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(
                EmberTableMaterialStats.ThresholdFor(stats.Health),
                Is.EqualTo(EmberTableMaterialStats.ReferenceThreshold));
        });
    }

    /// <summary>Bay adds half the reinforcement's integrity and all of its hardness.</summary>
    [Test]
    public void ReinforcementAddsHalfItsIntegrityAndAllOfItsHardness()
    {
        var stats = EmberTableMaterialStats.For(
            Material(integrity: 150, hardness: 60),
            Material(integrity: 150, hardness: 60));

        Assert.Multiple(() =>
        {
            Assert.That(stats.Health, Is.EqualTo(150f));
            Assert.That(stats.MinimumDamage, Is.EqualTo(12f));
            Assert.That(EmberTableMaterialStats.ThresholdFor(stats.Health), Is.EqualTo(250f));
        });
    }

    /// <summary>
    /// Bay's <c>round()</c> takes the floor rather than the nearest, so bronze's 25 hardness is a floor of two
    /// damage and not three. It matters: it is the smallest hit that registers at all.
    /// </summary>
    [Test]
    public void HardnessRoundsDownTheWayBaysRoundDoes()
    {
        Assert.That(EmberTableMaterialStats.For(Material(hardness: 25), null).MinimumDamage, Is.EqualTo(2f));
    }

    /// <summary>
    /// Bay multiplies damage to a brittle table fourfold, and reinforcing it with something that is not brittle
    /// takes that away again. More glass does not.
    /// </summary>
    [Test]
    public void BrittlePlatingGivesWayFourTimesAsFastUnlessSomethingSturdierHoldsIt()
    {
        var glass = Material(integrity: 50, hardness: 50, brittle: true);
        var steel = Material();

        Assert.Multiple(() =>
        {
            Assert.That(EmberTableMaterialStats.For(glass, null).DamageMultiplier,
                Is.EqualTo(EmberTableMaterialStats.BrittleMultiplier));
            Assert.That(EmberTableMaterialStats.For(glass, glass).DamageMultiplier,
                Is.EqualTo(EmberTableMaterialStats.BrittleMultiplier));
            Assert.That(EmberTableMaterialStats.For(glass, steel).DamageMultiplier, Is.EqualTo(1f));

            // A sturdy material stays sturdy no matter what is bolted to it.
            Assert.That(EmberTableMaterialStats.For(steel, glass).DamageMultiplier, Is.EqualTo(1f));
        });
    }

    /// <summary>A frame with nothing on it is a flat ten in Bay, and no hit is too small to count.</summary>
    [Test]
    public void ABareFrameIsWorthTenAndHasNoFloor()
    {
        var stats = EmberTableMaterialStats.For(null, null);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Health, Is.EqualTo(EmberTableMaterialStats.BareHealth));
            Assert.That(stats.MinimumDamage, Is.Zero);
            Assert.That(stats.DamageMultiplier, Is.EqualTo(1f));
        });
    }

#pragma warning disable RA0039 // The prototype is inert data here; nothing is looked up by id.
    private static EmberMaterialPrototype Material(
        int integrity = 150,
        int hardness = 60,
        bool brittle = false)
    {
        return new EmberMaterialPrototype
        {
            Key = "test",
            Integrity = integrity,
            Hardness = hardness,
            Brittle = brittle,
        };
    }
#pragma warning restore RA0039
}
