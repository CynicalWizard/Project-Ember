using Content.Shared.Ember.Ranks;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Ember.Ranks;

/// <summary>
/// Guards the plumbing around <see cref="HumanoidCharacterProfile.Branch"/> and
/// <see cref="HumanoidCharacterProfile.Rank"/>. A field left out of the copy constructor or of
/// <c>Equals</c> fails quietly — the character saves, and the value is simply gone next round —
/// so these are worth pinning even though the logic is trivial.
/// </summary>
[TestFixture]
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed class ProfileBranchAndRankTests
{
    private static readonly ProtoId<EmberBranchPrototype> Fleet = "EmberBranchFleet";
    private static readonly ProtoId<EmberBranchPrototype> Corps = "EmberBranchExpeditionaryCorps";
    private static readonly ProtoId<EmberRankPrototype> PettyOfficer = "EmberRankFleetE5";

    [Test]
    public void CopyConstructorCarriesBranchAndRank()
    {
        var original = new HumanoidCharacterProfile { Branch = Fleet, Rank = PettyOfficer };

        var copy = new HumanoidCharacterProfile(original);

        Assert.Multiple(() =>
        {
            Assert.That(copy.Branch, Is.EqualTo(original.Branch));
            Assert.That(copy.Rank, Is.EqualTo(original.Rank));
        });
    }

    [Test]
    public void CloneCarriesBranchAndRank()
    {
        var original = new HumanoidCharacterProfile { Branch = Fleet, Rank = PettyOfficer };

        var clone = original.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(clone.Branch, Is.EqualTo(Fleet));
            Assert.That(clone.Rank, Is.EqualTo(PettyOfficer));
        });
    }

    // A rank only means anything inside its own branch, so carrying it across is worse than
    // dropping it: the profile would hold a Fleet petty officer serving in the Corps.
    [Test]
    public void ChangingBranchDropsTheRank()
    {
        var profile = new HumanoidCharacterProfile { Branch = Fleet, Rank = PettyOfficer };

        var moved = profile.WithBranch(Corps);

        Assert.Multiple(() =>
        {
            Assert.That(moved.Branch, Is.EqualTo(Corps));
            Assert.That(moved.Rank, Is.Null);
        });
    }

    [Test]
    public void WithRankLeavesTheBranchAlone()
    {
        var profile = new HumanoidCharacterProfile { Branch = Fleet };

        var promoted = profile.WithRank(PettyOfficer);

        Assert.Multiple(() =>
        {
            Assert.That(promoted.Branch, Is.EqualTo(Fleet));
            Assert.That(promoted.Rank, Is.EqualTo(PettyOfficer));
        });
    }

    [Test]
    public void ProfilesDifferingOnlyByBranchAreNotEqual()
    {
        var fleet = new HumanoidCharacterProfile { Branch = Fleet };
        var corps = new HumanoidCharacterProfile { Branch = Corps };

        Assert.That(fleet.MemberwiseEquals(corps), Is.False);
    }

    [Test]
    public void ProfilesDifferingOnlyByRankAreNotEqual()
    {
        var unranked = new HumanoidCharacterProfile { Branch = Fleet };
        var ranked = new HumanoidCharacterProfile { Branch = Fleet, Rank = PettyOfficer };

        Assert.That(unranked.MemberwiseEquals(ranked), Is.False);
    }

    [Test]
    public void UnaffiliatedIsTheDefault()
    {
        var profile = new HumanoidCharacterProfile();

        Assert.Multiple(() =>
        {
            Assert.That(profile.Branch, Is.Null);
            Assert.That(profile.Rank, Is.Null);
        });
    }
}
