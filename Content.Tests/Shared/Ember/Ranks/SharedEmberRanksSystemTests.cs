using Content.Shared.Ember.Ranks;
using Content.Shared.Humanoid.Prototypes;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Ember.Ranks;

[TestFixture]
[TestOf(typeof(SharedEmberRanksSystem))]
public sealed class SharedEmberRanksSystemTests
{
    private static readonly ProtoId<SpeciesPrototype> Human = "Human";
    private static readonly ProtoId<SpeciesPrototype> Reptilian = "Reptilian";
    private static readonly ProtoId<SpeciesPrototype> Diona = "Diona";
    private static readonly ProtoId<SpeciesPrototype> Ipc = "IPC";

    #region Rank grading

    // Torch's convention, read back out in EmberRankPrototype: enlisted grades occupy 10..100,
    // commissioned start at 110, and zero means the rank is not graded at all.
    [Test]
    public void CategoryFollowsSortOrder()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MakeRank("civ").Category, Is.EqualTo(EmberRankCategory.None));
            Assert.That(MakeRank("e1", 10).Category, Is.EqualTo(EmberRankCategory.Enlisted));
            Assert.That(MakeRank("e9alt", 94).Category, Is.EqualTo(EmberRankCategory.Enlisted));
            Assert.That(MakeRank("o1", 110).Category, Is.EqualTo(EmberRankCategory.Commissioned));
            Assert.That(MakeRank("o10alt", 201).Category, Is.EqualTo(EmberRankCategory.Commissioned));
        });
    }

    [Test]
    public void GradeStringMatchesSierraFormat()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MakeRank("e5", 50).Grade, Is.EqualTo("E-5"));
            Assert.That(MakeRank("o3", 130).Grade, Is.EqualTo("O-3"));
            Assert.That(MakeRank("civ").Grade, Is.Empty);
        });
    }

    // An organisation whose ladder is not the SCG's should be able to say what its ranks are
    // without bending its numbers to land in the right class.
    [Test]
    public void CategoryOverrideBeatsSortOrder()
    {
        var rank = MakeRank("marshal", sortOrder: 40);
        rank.CategoryOverride = EmberRankCategory.Commissioned;

        Assert.Multiple(() =>
        {
            Assert.That(rank.Category, Is.EqualTo(EmberRankCategory.Commissioned));
            // The grade string is a reading of the SCG numbering, so an overridden rank has none.
            Assert.That(rank.Grade, Is.Empty);
        });
    }

    #endregion

    #region Branches and species

    [Test]
    public void BranchWithNoRestrictionsAdmitsAnySpecies()
    {
        var branch = MakeBranch("civilian");

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Reptilian), Is.True);
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, null), Is.True);
        });
    }

    [Test]
    public void BranchBlacklistBarsSpecies()
    {
        var branch = MakeBranch("fleet");
        branch.SpeciesBlacklist.Add(Reptilian);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Reptilian), Is.False);
        });
    }

    [Test]
    public void BranchWhitelistAdmitsOnlyListedSpecies()
    {
        var branch = MakeBranch("government");
        branch.SpeciesWhitelist.Add(Human);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Reptilian), Is.False);
        });
    }

    // A whitelist says who may join; a blacklist says who may not. Naming a species in both is
    // a mistake in the data, and the safe reading of a mistake is to refuse.
    [Test]
    public void BlacklistWinsOverWhitelist()
    {
        var branch = MakeBranch("confused");
        branch.SpeciesWhitelist.Add(Human);
        branch.SpeciesBlacklist.Add(Human);

        Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Human), Is.False);
    }

    // A character with no species chosen yet must not be filtered out of the branch list, or the
    // lobby would show an empty dropdown before the species is picked.
    [Test]
    public void NullSpeciesIgnoresBothLists()
    {
        var branch = MakeBranch("government");
        branch.SpeciesWhitelist.Add(Human);
        branch.SpeciesBlacklist.Add(Diona);

        Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, null), Is.True);
    }

    #endregion

    #region Ranks within a branch

    [Test]
    public void RankMustBelongToTheBranch()
    {
        var branch = MakeBranch("corps", "e3", "e5");
        var own = MakeRank("e3", 30);
        var foreign = MakeRank("fleet_e3", 30);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, own, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, foreign, Human), Is.False);
        });
    }

    // The Corps admits xeno personnel, but not above Senior Explorer: the restriction is on the
    // rank rather than the branch, which is how Bay's species_to_rank_whitelist works too.
    [Test]
    public void RankBlacklistBarsSpeciesTheBranchOtherwiseAdmits()
    {
        var branch = MakeBranch("corps", "e5", "o1");
        var enlisted = MakeRank("e5", 50);
        var officer = MakeRank("o1", 110);
        officer.SpeciesBlacklist.Add(Reptilian);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsBranchAllowed(branch, Reptilian), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, enlisted, Reptilian), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, officer, Reptilian), Is.False);
        });
    }

    [Test]
    public void RankWhitelistAdmitsOnlyListedSpecies()
    {
        var branch = MakeBranch("civilian", "unit");
        var unit = MakeRank("unit");
        unit.SpeciesWhitelist.Add(Ipc);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, unit, Ipc), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, unit, Human), Is.False);
        });
    }

    #endregion

    #region Selectable ranks

    // Admiral exists so events and admins can use it, not so that anyone spawns as one.
    [Test]
    public void SelectableRankMustBeInSpawnRanks()
    {
        var branch = MakeBranch("fleet", "e3", "o10");
        branch.SpawnRanks.Add("e3");
        var crewman = MakeRank("e3", 30);
        var admiral = MakeRank("o10", 200);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsRankSelectable(branch, crewman, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankSelectable(branch, admiral, Human), Is.False);
            // Still a legal rank to hold — an admin may assign it.
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, admiral, Human), Is.True);
        });
    }

    [Test]
    public void SelectableRankStillRespectsSpeciesRestrictions()
    {
        var branch = MakeBranch("corps", "o1");
        branch.SpawnRanks.Add("o1");
        var officer = MakeRank("o1", 110);
        officer.SpeciesBlacklist.Add(Reptilian);

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsRankSelectable(branch, officer, Human), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankSelectable(branch, officer, Reptilian), Is.False);
        });
    }

    // Bay leaves spawn_rank_types empty on branches where every rank is fair game. Treating an
    // empty list as "nothing is selectable" would silently empty the lobby dropdown.
    [Test]
    public void EmptySpawnRanksMeansEveryRankIsSelectable()
    {
        var branch = MakeBranch("civilian", "civ", "contractor");
        var civ = MakeRank("civ");

        Assert.That(SharedEmberRanksSystem.IsRankSelectable(branch, civ, Human), Is.True);
    }

    #endregion

    #region Age

    // The age brackets live on the rank, so a post that requires a commission inherits that
    // commission's age without anyone recomputing it per job.
    [Test]
    public void RankMinimumAgeIsEnforced()
    {
        var branch = MakeBranch("corps", "o6");
        var captain = MakeRank("o6", 160);
        captain.MinAge = 48;

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, captain, Human, 47), Is.False);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, captain, Human, 48), Is.True);
            Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, captain, Human, 60), Is.True);
        });
    }

    // The upper end of a bracket is what the service expects, not what it forbids — people do
    // stay in junior posts, and a hard ceiling would be the system saying they may not.
    [Test]
    public void RankMaximumAgeIsRecordedButNotEnforced()
    {
        var branch = MakeBranch("fleet", "e3");
        var crewman = MakeRank("e3", 30);
        crewman.MinAge = 19;
        crewman.MaxAge = 20;

        Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, crewman, Human, 55), Is.True);
    }

    // Age is optional so that callers with no character yet — the lobby before a profile loads —
    // are not silently shown an empty list.
    [Test]
    public void OmittedAgeSkipsTheCheck()
    {
        var branch = MakeBranch("corps", "o6");
        var captain = MakeRank("o6", 160);
        captain.MinAge = 48;

        Assert.That(SharedEmberRanksSystem.IsRankAllowed(branch, captain, Human), Is.True);
    }

    #endregion

    #region Service against employment

    // Serving in an organisation and drawing a company salary are alternatives, not layers. A
    // government posts its people; it does not hire them through a firm.
    [Test]
    public void ServiceBranchesTakeNoEmployer()
    {
        var corps = MakeBranch("corps");
        var civilian = MakeBranch("civilian");
        civilian.AllowsEmployer = true;

        Assert.Multiple(() =>
        {
            Assert.That(SharedEmberRanksSystem.AllowsEmployer(corps), Is.False);
            Assert.That(SharedEmberRanksSystem.AllowsEmployer(civilian), Is.True);
            // No branch at all is precisely the case of someone who works for a living.
            Assert.That(SharedEmberRanksSystem.AllowsEmployer(null), Is.True);
        });
    }

    // The trap this guards: the Expeditionary Corps is a government service, while the
    // Expeditionary Corps Organisation is a state-owned company. Being paid by the latter makes
    // someone a contractor, not a member of the former, however alike the two names look.
    [Test]
    public void TakingAPostingClearsTheEmployer()
    {
        var corps = MakeBranch("corps");
        var civilian = MakeBranch("civilian");
        civilian.AllowsEmployer = true;

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedEmberRanksSystem.ResolveEmployer(corps, "NanoTrasen"),
                Is.EqualTo(SharedEmberRanksSystem.NoEmployer));
            Assert.That(
                SharedEmberRanksSystem.ResolveEmployer(civilian, "NanoTrasen"),
                Is.EqualTo("NanoTrasen"));
            Assert.That(
                SharedEmberRanksSystem.ResolveEmployer(null, "NanoTrasen"),
                Is.EqualTo("NanoTrasen"));
        });
    }

    #endregion

    private static EmberRankPrototype MakeRank(string id, int sortOrder = 0)
    {
        return new EmberRankPrototype
        {
            ID = id,
            Name = $"ember-rank-{id}-name",
            SortOrder = sortOrder,
        };
    }

    private static EmberBranchPrototype MakeBranch(string id, params string[] ranks)
    {
        var branch = new EmberBranchPrototype
        {
            ID = id,
            Name = $"ember-branch-{id}-name",
            ShortName = $"ember-branch-{id}-short",
        };

        foreach (var rank in ranks)
        {
            branch.Ranks.Add(rank);
        }

        return branch;
    }
}
