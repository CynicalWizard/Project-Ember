using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Localization;
using Content.Shared.Ember.Ranks;
using Content.Shared.Ember.Roles;
using Content.Shared.Ember.Skills;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Enums;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Ember.Roles;

/// <summary>
/// A job may be held under more than one name. The names are mostly labels, but they are allowed
/// to narrow — by species, or by skill — and that is what has to be got right, because it is the
/// only way a variant can differ from its job without becoming a job.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedEmberJobTitleSystem))]
public sealed class SharedEmberJobTitleSystemTests
{
    private static readonly ProtoId<SpeciesPrototype> Human = "Human";
    private static readonly ProtoId<SpeciesPrototype> Reptilian = "Reptilian";

    #region Lookup

    [Test]
    public void ATitleIsFoundByItsId()
    {
        var job = MakeJob(MakeTitle("Electrician"), MakeTitle("AtmosphericTechnician"));

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberJobTitleSystem.TryGetTitle(job, "Electrician", out var title), Is.True);
            Assert.That(title!.Id, Is.EqualTo("Electrician"));
        });
    }

    // Null is not an error, it is the job's own name — the selection a player makes by not
    // making one.
    [Test]
    public void NoTitleMeansTheJobsOwnName()
    {
        var job = MakeJob(MakeTitle("Electrician"));

        Assert.That(SharedEmberJobTitleSystem.TryGetTitle(job, null, out _), Is.False);
    }

    // Ids are stored in profiles and so outlive the data that defined them. A title that was
    // renamed or removed has to read as "no title", not as a broken character.
    [Test]
    public void AnIdThatNoLongerExistsReadsAsNoTitle()
    {
        var job = MakeJob(MakeTitle("Electrician"));

        Assert.That(SharedEmberJobTitleSystem.TryGetTitle(job, "Welder", out _), Is.False);
    }

    #endregion

    #region Species

    [Test]
    public void ABlacklistedSpeciesCannotHoldTheTitle()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.SpeciesBlacklist.Add(Reptilian);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Human), Is.True);
            Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Reptilian), Is.False);
        });
    }

    // The case the whole feature was built for: a Unathi may be a doctor and may not be a
    // surgeon, and both of those are the same job.
    [Test]
    public void ARestrictedTitleDropsOutOfTheOfferedList()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.SpeciesBlacklist.Add(Reptilian);
        var job = MakeJob(surgeon, MakeTitle("Physician"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedEmberJobTitleSystem.GetSelectableTitles(job, Human).Select(t => t.Id),
                Is.EquivalentTo(new[] { "Surgeon", "Physician" }));
            Assert.That(
                SharedEmberJobTitleSystem.GetSelectableTitles(job, Reptilian).Select(t => t.Id),
                Is.EquivalentTo(new[] { "Physician" }));
        });
    }

    // Changing species should demote the character, not reject them. Sanitising returns null so
    // the doctor keeps the job under its own name.
    [Test]
    public void SanitizingDropsATitleTheSpeciesMayNotHold()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.SpeciesBlacklist.Add(Reptilian);
        var job = MakeJob(surgeon);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberJobTitleSystem.SanitizeTitle(job, "Surgeon", Human), Is.EqualTo("Surgeon"));
            Assert.That(SharedEmberJobTitleSystem.SanitizeTitle(job, "Surgeon", Reptilian), Is.Null);
            Assert.That(SharedEmberJobTitleSystem.SanitizeTitle(job, "Welder", Human), Is.Null);
        });
    }

    #endregion

    #region Age

    // A commission carries an education and a contract carries none, so a name that stands for
    // extra schooling has to say so itself. Surgeon over physician is the case.
    [Test]
    public void ATitleMayStandForYearsTheJobDoesNot()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.MinAge = 30;

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Human, 29), Is.False);
            Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Human, 30), Is.True);
            Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Human, 44), Is.True);
        });
    }

    // No upper bound, deliberately: a rank brackets its holders because a service expects
    // movement, and a qualification does not expire.
    [Test]
    public void AnOmittedAgeSkipsTheCheck()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.MinAge = 30;

        Assert.That(SharedEmberJobTitleSystem.IsTitleAllowed(surgeon, Human), Is.True);
    }

    [Test]
    public void TooYoungDropsOutOfTheOfferedList()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.MinAge = 30;
        var job = MakeJob(surgeon, MakeTitle("Physician"));

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedEmberJobTitleSystem.GetSelectableTitles(job, Human, 27).Select(t => t.Id),
                Is.EquivalentTo(new[] { "Physician" }));
            Assert.That(
                SharedEmberJobTitleSystem.GetSelectableTitles(job, Human, 31).Select(t => t.Id),
                Is.EquivalentTo(new[] { "Surgeon", "Physician" }));
        });
    }

    // Winding the age back demotes the surgeon to a doctor. It does not reject the character,
    // for the same reason changing species does not.
    [Test]
    public void SanitizingDropsATitleTheCharacterIsTooYoungFor()
    {
        var surgeon = MakeTitle("Surgeon");
        surgeon.MinAge = 30;
        var job = MakeJob(surgeon);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberJobTitleSystem.SanitizeTitle(job, "Surgeon", Human, 31), Is.EqualTo("Surgeon"));
            Assert.That(SharedEmberJobTitleSystem.SanitizeTitle(job, "Surgeon", Human, 22), Is.Null);
        });
    }

    #endregion

    #region Gendered forms

    // Russian has «медсестра» and «медбрат» where English has "nurse". Which one a character
    // gets follows from the character, and is never something the player picks.
    [Test]
    public void AGenderedNamePicksTheFormForTheCharacter()
    {
        var nurse = MakeTitle("Nurse");
        nurse.NameMale = "job-name-nurse-male";
        nurse.NameFemale = "job-name-nurse-female";

        Assert.Multiple(() =>
        {
            Assert.That(
                EmberGenderedName.Pick(nurse.Name, nurse.NameMale, nurse.NameFemale, Gender.Male),
                Is.EqualTo(new LocId("job-name-nurse-male")));
            Assert.That(
                EmberGenderedName.Pick(nurse.Name, nurse.NameMale, nurse.NameFemale, Gender.Female),
                Is.EqualTo(new LocId("job-name-nurse-female")));
        });
    }

    // Both genderless cases land on the neutral form, which is why the neutral form has to be
    // genuinely neutral rather than the feminine one doing double duty.
    [Test]
    public void EpiceneAndNeuterTakeTheNeutralForm()
    {
        var nurse = MakeTitle("Nurse");
        nurse.NameMale = "job-name-nurse-male";
        nurse.NameFemale = "job-name-nurse-female";

        Assert.Multiple(() =>
        {
            Assert.That(
                EmberGenderedName.Pick(nurse.Name, nurse.NameMale, nurse.NameFemale, Gender.Epicene),
                Is.EqualTo(nurse.Name));
            Assert.That(
                EmberGenderedName.Pick(nurse.Name, nurse.NameMale, nurse.NameFemale, Gender.Neuter),
                Is.EqualTo(nurse.Name));
        });
    }

    // The overwhelmingly common case: a name that needs no gendering says nothing and is used
    // as written, whoever holds it.
    [Test]
    public void AnUngenderedNameIsUsedAsWritten()
    {
        var electrician = MakeTitle("Electrician");

        foreach (var gender in new[] { Gender.Male, Gender.Female, Gender.Epicene, Gender.Neuter })
        {
            Assert.That(
                EmberGenderedName.Pick(electrician.Name, electrician.NameMale, electrician.NameFemale, gender),
                Is.EqualTo(electrician.Name));
        }
    }

    // One form may be given without the other — a language that distinguishes only one of them,
    // or data that has only got round to one so far.
    [Test]
    public void AMissingFormFallsBackRatherThanBreaking()
    {
        var title = MakeTitle("Nurse");
        title.NameFemale = "job-name-nurse-female";

        Assert.Multiple(() =>
        {
            Assert.That(
                EmberGenderedName.Pick(title.Name, title.NameMale, title.NameFemale, Gender.Male),
                Is.EqualTo(title.Name));
            Assert.That(
                EmberGenderedName.Pick(title.Name, title.NameMale, title.NameFemale, Gender.Female),
                Is.EqualTo(new LocId("job-name-nurse-female")));
        });
    }

    #endregion

    #region Skills

    // The Electrician asks for electrical work the Engineer does not. Without this the name would
    // be decoration, which is the failure mode worth guarding against.
    [Test]
    public void ATitleRaisesTheJobsFloor()
    {
        var electrical = MakeSkill("electrical");
        var electrician = MakeTitle("Electrician");
        electrician.MinSkills[electrical.ID] = SkillLevel.Trained;
        var job = MakeJob(electrician);
        job.MinSkills[electrical.ID] = SkillLevel.Basic;

        var generalist = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>
        {
            [electrical.ID] = SkillLevel.Basic,
        };

        Assert.Multiple(() =>
        {
            Assert.That(SharedSkillsSystem.MeetsRequirements(job, generalist), Is.True);
            Assert.That(
                SharedSkillsSystem.MeetsRequirements(job, generalist, branch: null, title: electrician),
                Is.False);
        });
    }

    // Three sources of floors — job, service, title — and the highest of them wins, whichever it
    // happens to be.
    [Test]
    public void TheHighestOfJobBranchAndTitleWins()
    {
        var eva = MakeSkill("EVA");
        var atmos = MakeSkill("atmos");
        var engines = MakeSkill("engines");

        var branch = new EmberBranchPrototype { MinSkills = { [eva.ID] = SkillLevel.Basic } };
        var title = MakeTitle("DamageControlTechnician");
        title.MinSkills[eva.ID] = SkillLevel.Trained;
        title.MinSkills[atmos.ID] = SkillLevel.Trained;

        var job = MakeJob(title);
        job.MinSkills[atmos.ID] = SkillLevel.Basic;
        job.MinSkills[engines.ID] = SkillLevel.Basic;

        var required = SharedSkillsSystem.GetRequiredSkills(job, branch, title);

        Assert.Multiple(() =>
        {
            Assert.That(required[eva.ID], Is.EqualTo(SkillLevel.Trained), "title beats branch");
            Assert.That(required[atmos.ID], Is.EqualTo(SkillLevel.Trained), "title beats job");
            Assert.That(required[engines.ID], Is.EqualTo(SkillLevel.Basic), "job stands alone");
        });
    }

    // Most titles add nothing. Passing one must not change the answer for those.
    [Test]
    public void ATitleWithNoSkillsChangesNothing()
    {
        var engines = MakeSkill("engines");
        var title = MakeTitle("Electrician");
        var job = MakeJob(title);
        job.MinSkills[engines.ID] = SkillLevel.Basic;

        Assert.That(
            SharedSkillsSystem.GetRequiredSkills(job, null, title),
            Is.EquivalentTo(SharedSkillsSystem.GetRequiredSkills(job, null)));
    }

    [Test]
    public void MergingLeavesTheTitleAlone()
    {
        var atmos = MakeSkill("atmos");
        var title = MakeTitle("AtmosphericTechnician");
        title.MinSkills[atmos.ID] = SkillLevel.Trained;
        var job = MakeJob(title);

        SharedSkillsSystem.GetRequiredSkills(job, null, title);

        Assert.That(title.MinSkills, Has.Count.EqualTo(1));
    }

    #endregion

    private static EmberJobTitle MakeTitle(string id)
    {
        return new EmberJobTitle
        {
            Id = id,
            Name = $"job-name-ember-{id}",
        };
    }

    private static JobPrototype MakeJob(params EmberJobTitle[] titles)
    {
        var job = new JobPrototype();

        foreach (var title in titles)
        {
            job.AltTitles.Add(title);
        }

        return job;
    }

    private static SkillPrototype MakeSkill(string id, int difficulty = 1)
    {
        return new SkillPrototype
        {
            ID = id,
            Name = $"skill-{id}-name",
            Difficulty = difficulty,
        };
    }
}
