#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Background;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Coercing an invalid background must land on something the species can actually hold.
/// </summary>
/// <remarks>
/// This is a regression guard, and the bug it guards is worth stating because it did not look like
/// a bug in the data at all. The default homeworld is Mars, and Mars is human and IPC only. Hand it
/// to a tajaran whose stored homeworld had gone invalid and the correction is invalid too - so the
/// lobby corrected it again, and again, and hung the client with a stack of alternating
/// RefreshBackground and SetBackground frames.
///
/// The assertion is therefore not "Resolve returns the default" but "Resolve returns something
/// selectable", checked for every species against every axis. That is the property the recursion
/// needed and did not have; a test of the Mars case alone would pass again the moment somebody
/// gave another default a species requirement.
/// </remarks>
[TestFixture]
public sealed class EmberBackgroundResolveTest
{
    private static readonly (EmberBackgroundAxis Axis, string Default)[] Axes =
    {
        (EmberBackgroundAxis.Homeworld, SharedHumanoidAppearanceSystem.DefaultHomeworld),
        (EmberBackgroundAxis.Culture, SharedHumanoidAppearanceSystem.DefaultCulture),
        (EmberBackgroundAxis.Faction, SharedHumanoidAppearanceSystem.DefaultFaction),
        (EmberBackgroundAxis.Religion, SharedHumanoidAppearanceSystem.DefaultReligion),
    };

    [Test]
    public async Task EverySpeciesResolvesToSomethingItCanHold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var species = protoMan.EnumeratePrototypes<SpeciesPrototype>().ToList();
            Assert.That(species, Is.Not.Empty, "No species to check against.");

            Assert.Multiple(() =>
            {
                foreach (var proto in species)
                {
                    foreach (var (axis, fallback) in Axes)
                    {
                        // "EmberNoSuchThing" stands in for any stored id that has gone invalid:
                        // deleted, moved to another axis, or barred by a species change.
                        var resolved = SharedEmberBackgroundSystem.Resolve(
                            protoMan, "EmberNoSuchThing", axis, proto.ID, fallback);

                        Assert.That(
                            SharedEmberBackgroundSystem.IsValidFor(protoMan, resolved, axis, proto.ID),
                            Is.True,
                            $"{proto.ID} on {axis} resolved to {resolved.Id}, which it cannot hold. " +
                            "Coercing to something invalid is what made the lobby correct its own " +
                            "correction forever.");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Resolving twice changes nothing the second time.
    /// </summary>
    /// <remarks>
    /// The direct expression of what the lobby needs: it corrects, redraws, and checks again, so a
    /// correction that is itself corrected is an endless loop no matter how the redraw is written.
    /// </remarks>
    [Test]
    public async Task ResolvingIsStable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var checks = new List<string>();

            Assert.Multiple(() =>
            {
                foreach (var proto in protoMan.EnumeratePrototypes<SpeciesPrototype>())
                {
                    foreach (var (axis, fallback) in Axes)
                    {
                        var once = SharedEmberBackgroundSystem.Resolve(
                            protoMan, "EmberNoSuchThing", axis, proto.ID, fallback);
                        var twice = SharedEmberBackgroundSystem.Resolve(
                            protoMan, once, axis, proto.ID, fallback);

                        Assert.That(twice, Is.EqualTo(once),
                            $"{proto.ID} on {axis}: resolving {once.Id} again gave {twice.Id}.");

                        checks.Add($"{proto.ID}/{axis}");
                    }
                }
            });

            Assert.That(checks, Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
