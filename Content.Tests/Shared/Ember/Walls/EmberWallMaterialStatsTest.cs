using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Walls;

/// <summary>
/// Bay's calculate_damage_data, pinned. These numbers decide whether a wall is a speed bump or a real obstacle,
/// and every one of them is a fixed constant lifted from Bay rather than something tuned here.
/// </summary>
[TestFixture]
[TestOf(typeof(EmberWallMaterialStats))]
public sealed class EmberWallMaterialStatsTest
{
    /// <summary>
    /// Steel is the material every other one is measured against: integrity 150, hardness 60, armour 7.
    /// </summary>
    [Test]
    public void SteelMatchesTheReferenceWall()
    {
        var stats = EmberWallMaterialStats.For(Material(integrity: 150, hardness: 60, armor: 7), null);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Integrity, Is.EqualTo(EmberWallMaterialStats.ReferenceIntegrity));
            Assert.That(stats.MinimumDamage, Is.EqualTo(16f)); // round(60 * 2.6 / 10)
            Assert.That(stats.BruteCoefficient, Is.EqualTo(1f)); // steel is the anchor, so it is left alone
            Assert.That(stats.ExplosionCoefficient, Is.EqualTo(1f)); // steel's 5 is the break-even point
        });
    }

    [Test]
    public void ReinforcementAddsThreeQuartersOfItsIntegrityAndAllOfItsArmour()
    {
        var plain = Material(integrity: 150, hardness: 60, armor: 7);
        var stats = EmberWallMaterialStats.For(plain, Material(integrity: 400, hardness: 80, armor: 8));

        Assert.Multiple(() =>
        {
            Assert.That(stats.Integrity, Is.EqualTo(225f + 300f));
            Assert.That(stats.MinimumDamage, Is.EqualTo(31f)); // round((60 * 2.6 + round(80 * 1.9)) / 10)
            Assert.That(stats.BruteCoefficient, Is.EqualTo(7f / 15f).Within(0.001f));
        });
    }

    /// <summary>
    /// The point of anchoring to steel is that the ratios between materials stay exactly Bay's: wood, at one
    /// point of armour against steel's seven, still takes seven times what steel does.
    /// </summary>
    [Test]
    public void MaterialsKeepTheirRatiosToSteel()
    {
        var wood = EmberWallMaterialStats.For(Material(armor: 1), null);
        var titanium = EmberWallMaterialStats.For(Material(armor: 10), null);

        Assert.Multiple(() =>
        {
            Assert.That(wood.BruteCoefficient, Is.EqualTo(7f).Within(0.001f));
            Assert.That(titanium.BruteCoefficient, Is.EqualTo(0.7f).Within(0.001f));
        });
    }

    /// <summary>
    /// Bay divides by the armour value and would make an unarmoured wall immune. Nothing ported has zero armour,
    /// but the guard should read as "flimsiest wall", not "invincible".
    /// </summary>
    [Test]
    public void NoArmourIsTheWeakestWallRatherThanTheStrongest()
    {
        var stats = EmberWallMaterialStats.For(Material(armor: 0), null);

        Assert.Multiple(() =>
        {
            Assert.That(stats.BruteCoefficient, Is.EqualTo(EmberWallMaterialStats.ReferenceArmor));
            Assert.That(stats.BurnCoefficient, Is.EqualTo(EmberWallMaterialStats.ReferenceArmor));
        });
    }

    /// <summary>
    /// Bay draws no overlay on an unmarked wall and the full one on a wall about to fall over.
    /// </summary>
    [Test]
    public void DamageOverlayRunsFromInvisibleToOpaque()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberWallMaterialStats.GetDamageOverlayAlpha(0f), Is.Zero);
            Assert.That(EmberWallMaterialStats.GetDamageOverlayAlpha(1f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                EmberWallMaterialStats.GetDamageOverlayAlpha(0.5f),
                Is.GreaterThan(EmberWallMaterialStats.GetDamageOverlayAlpha(0.1f)));
        });
    }

    /// <summary>
    /// A tougher material takes less from a blast, and the wall takes the better of its two rather than adding
    /// them up.
    /// </summary>
    [Test]
    public void ExplosionResistanceTakesTheStrongerMaterial()
    {
        var steel = Material(explosionResistance: 5f);
        var plasteel = Material(explosionResistance: 7.5f);

        Assert.Multiple(() =>
        {
            Assert.That(EmberWallMaterialStats.For(steel, plasteel).ExplosionCoefficient,
                Is.EqualTo(5f / 7.5f).Within(0.001f));
            Assert.That(EmberWallMaterialStats.For(plasteel, steel).ExplosionCoefficient,
                Is.EqualTo(5f / 7.5f).Within(0.001f));
        });
    }

    /// <summary>
    /// Bay: <c>material.radioactivity + reinf_material.radioactivity / 2</c>.
    /// </summary>
    [Test]
    public void ReinforcementContributesHalfItsRadioactivity()
    {
        var inert = Material();
        var uranium = Material(radioactivity: 12f);

        Assert.Multiple(() =>
        {
            Assert.That(EmberWallMaterialStats.For(inert, null).Radioactivity, Is.Zero);
            Assert.That(EmberWallMaterialStats.For(uranium, null).Radioactivity, Is.EqualTo(12f));
            Assert.That(EmberWallMaterialStats.For(inert, uranium).Radioactivity, Is.EqualTo(6f));
            Assert.That(EmberWallMaterialStats.For(uranium, uranium).Radioactivity, Is.EqualTo(18f));
        });
    }

#pragma warning disable RA0039 // The prototype is inert data here; nothing is looked up by id.
    private static EmberMaterialPrototype Material(
        int integrity = 150,
        int hardness = 60,
        int armor = 7,
        float explosionResistance = 5f,
        float? radioactivity = null)
    {
        return new EmberMaterialPrototype
        {
            Key = "test",
            Integrity = integrity,
            Hardness = hardness,
            BruteArmor = armor,
            BurnArmor = armor,
            ExplosionResistance = explosionResistance,
            Radioactivity = radioactivity,
        };
    }
#pragma warning restore RA0039
}
