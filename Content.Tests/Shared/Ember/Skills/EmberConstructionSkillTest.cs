using Content.Shared.Ember.Skills;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Skills;

/// <summary>
/// The construction skill curve is Bay's, and both the server that rolls the outcome and the menu that warns
/// about it beforehand read these numbers, so they are worth pinning down.
/// </summary>
[TestFixture]
[TestOf(typeof(EmberConstructionSkill))]
public sealed class EmberConstructionSkillTest
{
    /// <summary>
    /// skill_delay_mult: Trained builds in the listed time, every level below adds 30% and every level above
    /// takes 30% off.
    /// </summary>
    [Test]
    [TestCase(SkillLevel.Unskilled, 1.6f)]
    [TestCase(SkillLevel.Basic, 1.3f)]
    [TestCase(SkillLevel.Trained, 1.0f)]
    [TestCase(SkillLevel.Experienced, 0.7f)]
    [TestCase(SkillLevel.Master, 0.4f)]
    public void DelayMultiplierMatchesTheBayCurve(SkillLevel level, float expected)
    {
        Assert.That(SharedSkillsSystem.GetDelayMultiplier(level), Is.EqualTo(expected).Within(0.0001f));
    }

    /// <summary>
    /// skill_fail_chance: 90% at Unskilled, halving per level. Bay passes the recipe difficulty as the level
    /// where failure stops entirely, which is what makes an easy recipe safe for anyone.
    /// </summary>
    [Test]
    [TestCase(SkillLevel.Unskilled, 90)]
    [TestCase(SkillLevel.Basic, 45)]
    [TestCase(SkillLevel.Trained, 22)] // 22.5 rounds to even
    public void FailChanceHalvesPerLevel(SkillLevel level, int expected)
    {
        var chance = SharedSkillsSystem.GetFailChance(
            level,
            EmberConstructionSkill.UnskilledFailChance,
            SkillLevel.Master);

        Assert.That(chance, Is.EqualTo(expected));
    }

    [Test]
    public void ReachingTheRequiredLevelRemovesFailureEntirely()
    {
        var required = EmberConstructionSkill.GetRequiredLevel(2);

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedSkillsSystem.GetFailChance(required, EmberConstructionSkill.UnskilledFailChance, required),
                Is.Zero);
            Assert.That(
                SharedSkillsSystem.GetFailChance((SkillLevel) (required - 1), EmberConstructionSkill.UnskilledFailChance, required),
                Is.Not.Zero);
        });
    }

    /// <summary>
    /// Difficulty is measured on the skill scale so it can be handed straight to the failure roll, so it has to
    /// stay inside it even for the hardest material.
    /// </summary>
    [Test]
    [TestCase(0, SkillLevel.Unskilled)]
    [TestCase(1, SkillLevel.Unskilled)]
    [TestCase(2, SkillLevel.Basic)]
    [TestCase(3, SkillLevel.Trained)]
    [TestCase(9, SkillLevel.Master)]
    public void RequiredLevelStaysOnTheSkillScale(int difficulty, SkillLevel expected)
    {
        Assert.That(EmberConstructionSkill.GetRequiredLevel(difficulty), Is.EqualTo(expected));
    }
}
