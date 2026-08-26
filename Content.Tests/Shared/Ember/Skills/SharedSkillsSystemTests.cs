using System.Collections.Generic;
using Content.Shared.Ember.Ranks;
using Content.Shared.Ember.Skills;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Ember.Skills;

/// <summary>
/// A character carries one set of skills, not one per job they might take. A job no longer
/// grants points or caps anything — it states what it requires, and the character either meets
/// that or does not.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedSkillsSystem))]
public sealed class SharedSkillsSystemTests
{
    #region Cost

    // Bay's curve: the first two levels cost the skill's difficulty each, the top two cost
    // double. A hard skill is hard all the way up, and the last stretch is the expensive one.
    [Test]
    public void LevelCostMatchesSierraCostCurve()
    {
        var skill = MakeSkill("electrical", difficulty: 2);

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.GetLevelCost(skill, SkillLevel.Unskilled), Is.EqualTo(0));
            Assert.That(SharedSkillsSystem.GetLevelCost(skill, SkillLevel.Basic), Is.EqualTo(2));
            Assert.That(SharedSkillsSystem.GetLevelCost(skill, SkillLevel.Trained), Is.EqualTo(2));
            Assert.That(SharedSkillsSystem.GetLevelCost(skill, SkillLevel.Experienced), Is.EqualTo(4));
            Assert.That(SharedSkillsSystem.GetLevelCost(skill, SkillLevel.Master), Is.EqualTo(4));
        });
    }

    // Reaching a level costs every step up to it, since there is no job minimum to start from
    // any more. Unskilled is where everyone begins and is free.
    [Test]
    public void TotalCostAccumulatesFromUnskilled()
    {
        var skill = MakeSkill("medical", difficulty: 1);

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.GetTotalCost(skill, SkillLevel.Unskilled), Is.EqualTo(0));
            Assert.That(SharedSkillsSystem.GetTotalCost(skill, SkillLevel.Basic), Is.EqualTo(1));
            Assert.That(SharedSkillsSystem.GetTotalCost(skill, SkillLevel.Trained), Is.EqualTo(2));
            Assert.That(SharedSkillsSystem.GetTotalCost(skill, SkillLevel.Experienced), Is.EqualTo(4));
            Assert.That(SharedSkillsSystem.GetTotalCost(skill, SkillLevel.Master), Is.EqualTo(6));
        });
    }

    [Test]
    public void SpentPointsSumsEverySkill()
    {
        var medical = MakeSkill("medical", difficulty: 1);
        var engines = MakeSkill("engines", difficulty: 2);
        var allocation = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [medical.ID] = SkillLevel.Trained, // 1 + 1
            [engines.ID] = SkillLevel.Basic, // 2
        };

        Assert.That(
            SharedSkillsSystem.GetSpentPoints(new[] { medical, engines }, allocation),
            Is.EqualTo(4));
    }

    #endregion

    #region Budget

    // The budget belongs to the character, not to the job: one person, one allowance, spent
    // once. Age is the only thing that adds to it, which is how time served turns into skill
    // without a separate experience system.
    [Test]
    public void BudgetIsBasePlusAge()
    {
        var species = MakeSpecies(youngAge: 30, oldAge: 60);

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedSkillsSystem.GetSkillPointBudget(null, 25),
                Is.EqualTo(SharedSkillsSystem.BaseSkillPoints));
            Assert.That(
                SharedSkillsSystem.GetSkillPointBudget(species, 60),
                Is.GreaterThan(SharedSkillsSystem.GetSkillPointBudget(species, 25)));
        });
    }

    [Test]
    public void ConfiguredAgeBracketsBeatTheDefaultCurve()
    {
        var species = MakeSpecies(youngAge: 30, oldAge: 60);
        species.SkillAgePoints.Add(new SkillAgePointBracket { MinimumAge = 0, Points = 0 });
        species.SkillAgePoints.Add(new SkillAgePointBracket { MinimumAge = 40, Points = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.GetAgeSkillPoints(species, 39), Is.EqualTo(0));
            Assert.That(SharedSkillsSystem.GetAgeSkillPoints(species, 40), Is.EqualTo(10));
        });
    }

    #endregion

    #region Sanitising

    [Test]
    public void AllocationIsCappedByTheSkillsOwnMaximum()
    {
        var skill = MakeSkill("exosuit", defaultMax: SkillLevel.Trained);
        var allocation = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [skill.ID] = SkillLevel.Master,
        };

        var clean = SharedSkillsSystem.SanitizeAllocation(new[] { skill }, allocation, 100);

        Assert.That(clean[skill.ID], Is.EqualTo(SkillLevel.Trained));
    }

    // Prerequisites are checked against the finished set, so dropping one skill can invalidate
    // another. The loop has to run until nothing changes rather than once over the list.
    [Test]
    public void UnmetPrerequisiteDropsTheDependentSkill()
    {
        var eva = MakeSkill("EVA");
        var exosuit = MakeSkill("exosuit");
        exosuit.Prerequisites[eva.ID] = SkillLevel.Trained;

        var allocation = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [eva.ID] = SkillLevel.Basic,
            [exosuit.ID] = SkillLevel.Trained,
        };

        var clean = SharedSkillsSystem.SanitizeAllocation(new[] { eva, exosuit }, allocation, 100);

        Assert.Multiple(() =>
        {
            Assert.That(clean[eva.ID], Is.EqualTo(SkillLevel.Basic));
            Assert.That(clean[exosuit.ID], Is.EqualTo(SkillLevel.Unskilled));
        });
    }

    [Test]
    public void AllocationBeyondTheBudgetIsDropped()
    {
        var cheap = MakeSkill("hauling", difficulty: 1);
        var dear = MakeSkill("engines", difficulty: 2);
        var allocation = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [cheap.ID] = SkillLevel.Basic, // 1
            [dear.ID] = SkillLevel.Master, // 2 + 2 + 4 + 4
        };

        var clean = SharedSkillsSystem.SanitizeAllocation(new[] { cheap, dear }, allocation, 3);

        Assert.Multiple(() =>
        {
            Assert.That(clean[cheap.ID], Is.EqualTo(SkillLevel.Basic));
            Assert.That(clean[dear.ID], Is.EqualTo(SkillLevel.Unskilled));
            Assert.That(
                SharedSkillsSystem.GetRemainingPoints(new[] { cheap, dear }, clean, 3),
                Is.EqualTo(2));
        });
    }

    [Test]
    public void SkillsAbsentFromTheAllocationAreUnskilled()
    {
        var skill = MakeSkill("virology");

        var clean = SharedSkillsSystem.SanitizeAllocation(
            new[] { skill },
            new Dictionary<ProtoId<SkillPrototype>, SkillLevel>(),
            100);

        Assert.That(clean[skill.ID], Is.EqualTo(SkillLevel.Unskilled));
    }

    #endregion

    #region Job requirements

    // A job no longer hands out free levels. It states a floor, and a character either clears it
    // or cannot take the job — which is what makes overlapping roles fall out of the data
    // instead of being listed by hand.
    [Test]
    public void JobRequirementsAreMetOnlyWhenEverySkillClearsTheFloor()
    {
        var medical = MakeSkill("medical");
        var anatomy = MakeSkill("anatomy");
        var job = new JobPrototype
        {
            MinSkills =
            {
                [medical.ID] = SkillLevel.Trained,
                [anatomy.ID] = SkillLevel.Basic,
            },
        };

        var qualified = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [medical.ID] = SkillLevel.Experienced,
            [anatomy.ID] = SkillLevel.Basic,
        };
        var short_ = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [medical.ID] = SkillLevel.Basic,
            [anatomy.ID] = SkillLevel.Master,
        };

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.MeetsRequirements(job, qualified), Is.True);
            Assert.That(SharedSkillsSystem.MeetsRequirements(job, short_), Is.False);
        });
    }

    [Test]
    public void JobWithNoRequirementsTakesAnyone()
    {
        var job = new JobPrototype();

        Assert.That(
            SharedSkillsSystem.MeetsRequirements(job, new Dictionary<ProtoId<SkillPrototype>, SkillLevel>()),
            Is.True);
    }

    // The janitor and the cook overlap because neither asks for anything; the paramedic and the
    // field medic overlap because they ask for the same things. Neither pairing is written down
    // anywhere — both fall out of the requirements.
    [Test]
    public void OverlappingRolesFallOutOfSharedRequirements()
    {
        var medical = MakeSkill("medical");
        var eva = MakeSkill("EVA");
        var paramedic = new JobPrototype
        {
            MinSkills = { [medical.ID] = SkillLevel.Basic, [eva.ID] = SkillLevel.Trained },
        };
        var fieldMedic = new JobPrototype
        {
            MinSkills = { [medical.ID] = SkillLevel.Basic, [eva.ID] = SkillLevel.Basic },
        };
        var surgeon = new JobPrototype
        {
            MinSkills = { [medical.ID] = SkillLevel.Experienced },
        };

        var character = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [medical.ID] = SkillLevel.Basic,
            [eva.ID] = SkillLevel.Trained,
        };

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.MeetsRequirements(paramedic, character), Is.True);
            Assert.That(SharedSkillsSystem.MeetsRequirements(fieldMedic, character), Is.True);
            Assert.That(SharedSkillsSystem.MeetsRequirements(surgeon, character), Is.False);
        });
    }

    #endregion

    #region Branch floors

    // The reason a branch carries minSkills at all: it states once what everyone in it is
    // expected to know, and thirty job prototypes do not each repeat the same three lines. That
    // only holds if the two are actually merged, which is what these cover.
    [Test]
    public void BranchFloorIsAddedToTheJobsOwn()
    {
        var eva = MakeSkill("EVA");
        var forensics = MakeSkill("forensics");
        var branch = new EmberBranchPrototype { MinSkills = { [eva.ID] = SkillLevel.Basic } };
        var job = new JobPrototype { MinSkills = { [forensics.ID] = SkillLevel.Trained } };

        var investigator = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [forensics.ID] = SkillLevel.Trained,
        };

        Assert.Multiple(() =>
        {
            // Qualified for the work, and would have passed before the branch was consulted.
            Assert.That(SharedSkillsSystem.MeetsRequirements(job, investigator), Is.True);
            // Still cannot serve in a branch that expects everyone to manage a voidsuit.
            Assert.That(SharedSkillsSystem.MeetsRequirements(job, investigator, branch), Is.False);
        });
    }

    // Both sides may name the same skill. The higher of the two wins, in either direction: a
    // service's floor never lowers a post's demand, and a post never excuses someone from what
    // their service expects.
    [Test]
    public void TheHigherOfTheTwoFloorsWins()
    {
        var weapons = MakeSkill("weapons");
        var branch = new EmberBranchPrototype { MinSkills = { [weapons.ID] = SkillLevel.Basic } };
        var masterAtArms = new JobPrototype { MinSkills = { [weapons.ID] = SkillLevel.Trained } };
        var corpsman = new JobPrototype();

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedSkillsSystem.GetRequiredSkills(masterAtArms, branch)[weapons.ID],
                Is.EqualTo(SkillLevel.Trained));
            Assert.That(
                SharedSkillsSystem.GetRequiredSkills(corpsman, branch)[weapons.ID],
                Is.EqualTo(SkillLevel.Basic));
        });
    }

    // A civilian has no service to expect anything of them, so the job's own floors are the
    // whole of it.
    [Test]
    public void NoBranchMeansNoExtraFloor()
    {
        var eva = MakeSkill("EVA");
        var job = new JobPrototype { MinSkills = { [eva.ID] = SkillLevel.Basic } };

        Assert.That(SharedSkillsSystem.GetRequiredSkills(job, null), Has.Count.EqualTo(1));
    }

    // The security cadet: asked for nothing beyond being in the Fleet. The job states no
    // minimums of its own, and the check is still not a no-op.
    [Test]
    public void AJobWithNoMinimumsStillAnswersToItsBranch()
    {
        var eva = MakeSkill("EVA");
        var branch = new EmberBranchPrototype { MinSkills = { [eva.ID] = SkillLevel.Basic } };
        var cadet = new JobPrototype();

        var recruit = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>();

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.MeetsRequirements(cadet, recruit), Is.True);
            Assert.That(SharedSkillsSystem.MeetsRequirements(cadet, recruit, branch), Is.False);
        });
    }

    // Merging must not write back into the prototypes it read from.
    [Test]
    public void MergingLeavesBothPrototypesAlone()
    {
        var eva = MakeSkill("EVA");
        var forensics = MakeSkill("forensics");
        var branch = new EmberBranchPrototype { MinSkills = { [eva.ID] = SkillLevel.Basic } };
        var job = new JobPrototype { MinSkills = { [forensics.ID] = SkillLevel.Trained } };

        SharedSkillsSystem.GetRequiredSkills(job, branch);

        Assert.Multiple(() =>
        {
            Assert.That(job.MinSkills, Has.Count.EqualTo(1));
            Assert.That(branch.MinSkills, Has.Count.EqualTo(1));
        });
    }

    #endregion

    private static SkillPrototype MakeSkill(
        string id,
        int difficulty = 1,
        SkillLevel defaultMax = SkillLevel.Master)
    {
        return new SkillPrototype
        {
            ID = id,
            Category = "test",
            Name = $"skill-{id}-name",
            Description = $"skill-{id}-desc",
            Difficulty = difficulty,
            DefaultMax = defaultMax,
            Levels = new List<string>
            {
                "skill-level-unskilled",
                "skill-level-basic",
                "skill-level-trained",
                "skill-level-experienced",
                "skill-level-master",
            },
        };
    }

    private static SpeciesPrototype MakeSpecies(int youngAge, int oldAge)
    {
        return new SpeciesPrototype { YoungAge = youngAge, OldAge = oldAge };
    }
}
