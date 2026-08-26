using System.Linq;
using Content.Shared.Ember.Clothing;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Clothing;

/// <summary>
/// A uniform has three worn states, not four: ordinary, sleeves up, pulled down. The rules that
/// keep the fourth from existing are SierraBay12's, and they are what makes two verbs feel like
/// two switches rather than a cycle.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedEmberRollableClothingSystem))]
public sealed class EmberRollableClothingTests
{
    private const bool Both = true;

    private static EmberClothingRoll? Resolve(
        EmberClothingRoll current,
        EmberClothingRoll requested,
        bool sleeves = Both,
        bool down = Both)
    {
        return SharedEmberRollableClothingSystem.Resolve(current, requested, sleeves, down);
    }

    [Test]
    public void OrdinaryTransitionsJustHappen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Resolve(EmberClothingRoll.None, EmberClothingRoll.Sleeves), Is.EqualTo(EmberClothingRoll.Sleeves));
            Assert.That(Resolve(EmberClothingRoll.None, EmberClothingRoll.Down), Is.EqualTo(EmberClothingRoll.Down));
            Assert.That(Resolve(EmberClothingRoll.Sleeves, EmberClothingRoll.None), Is.EqualTo(EmberClothingRoll.None));
            Assert.That(Resolve(EmberClothingRoll.Down, EmberClothingRoll.None), Is.EqualTo(EmberClothingRoll.None));
        });
    }

    // There are no sleeves on the arms to roll when the garment is round your waist. Refused,
    // and the player is told why.
    [Test]
    public void SleevesCannotBeRolledWhilePulledDown()
    {
        Assert.That(Resolve(EmberClothingRoll.Down, EmberClothingRoll.Sleeves), Is.Null);
    }

    // The other direction is a step rather than a refusal: the first press unrolls the sleeves
    // and leaves the garment ordinary, the second pulls it down.
    [Test]
    public void PullingDownFromRolledSleevesPassesThroughOrdinary()
    {
        Assert.That(
            Resolve(EmberClothingRoll.Sleeves, EmberClothingRoll.Down),
            Is.EqualTo(EmberClothingRoll.None));
    }

    // A skirt has no sleeves; Bay marks that with rolled_sleeves = -1 and we with a flag.
    [Test]
    public void AGarmentRefusesWhatItHasNoArtworkFor()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Resolve(EmberClothingRoll.None, EmberClothingRoll.Sleeves, sleeves: false), Is.Null);
            Assert.That(Resolve(EmberClothingRoll.None, EmberClothingRoll.Down, down: false), Is.Null);
            // ...but the one it does have still works.
            Assert.That(
                Resolve(EmberClothingRoll.None, EmberClothingRoll.Down, sleeves: false),
                Is.EqualTo(EmberClothingRoll.Down));
        });
    }

    // Asking for the state you are already in is not a transition, so nothing is dirtied and no
    // sprite is rebuilt.
    [Test]
    public void AskingForTheCurrentStateDoesNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Resolve(EmberClothingRoll.None, EmberClothingRoll.None), Is.Null);
            Assert.That(Resolve(EmberClothingRoll.Sleeves, EmberClothingRoll.Sleeves), Is.Null);
            Assert.That(Resolve(EmberClothingRoll.Down, EmberClothingRoll.Down), Is.Null);
        });
    }

    // The fourth state must be unreachable from anywhere: no sequence of requests leaves the
    // garment both rolled and pulled down, because the enum cannot express it and the rules
    // never route through a state the caller did not ask for except the ordinary one.
    [Test]
    public void EveryReachableStateIsOneOfTheThree()
    {
        var states = new[] { EmberClothingRoll.None, EmberClothingRoll.Sleeves, EmberClothingRoll.Down };

        foreach (var current in states)
        {
            foreach (var requested in states)
            {
                var result = Resolve(current, requested);
                Assert.That(result == null || states.Contains(result.Value), Is.True,
                    $"{current} -> {requested} left the garment somewhere unexpected");
            }
        }
    }
}
