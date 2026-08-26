#nullable enable
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A uniform belongs to the service and the tools belong to the post. Each Ember job kit inherits
/// its tools from the kit it replaced and hangs the four services off it as sub-gear, so the
/// engineer's coverall is Corps black or contractor grey and the toolbelt is the same either way.
/// </summary>
/// <remarks>
/// The failure this guards against is silent in both directions. A sub-gear whose requirements
/// never pass leaves the post's vanilla jumpsuit on, which looks like a uniform that simply has
/// not been converted yet; a sub-gear that names a slot the post also needs quietly takes the
/// post's item away, and a missing toolbelt reads as a mapping problem rather than a kit one.
///
/// Neither is caught by the YAML linter, which checks that the prototypes resolve and nothing
/// about which of them wins.
/// </remarks>
[TestFixture]
public sealed class EmberBranchUniformTest
{
    private const string JumpsuitSlot = "jumpsuit";

    [Test]
    public async Task TheServiceDecidesTheUniformAndThePostDecidesTheRest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var spawning = entMan.System<SharedStationSpawningSystem>();

        await server.WaitAssertion(() =>
        {
            var engineer = protoMan.Index<JobPrototype>("EmberEngineer");
            var captain = protoMan.Index<JobPrototype>("EmberCaptain");

            var corps = Kit(protoMan, spawning, engineer, "EmberBranchExpeditionaryCorps", "EmberRankCorpsE5");
            var contractor = Kit(protoMan, spawning, engineer, "EmberBranchCivilian", "EmberRankContractor");
            var master = Kit(protoMan, spawning, captain, "EmberBranchExpeditionaryCorps", "EmberRankCorpsO5");

            Assert.Multiple(() =>
            {
                Assert.That(corps.GetGear(JumpsuitSlot), Is.EqualTo("EmberClothingUniformUtilityExpeditionary"),
                    "A rating of the Corps should be in the Corps coverall.");

                Assert.That(contractor.GetGear(JumpsuitSlot), Is.EqualTo("EmberClothingUniformUtility"),
                    "A contractor in the same post should be in the undyed one.");

                // The gold trim follows the commission rather than the billet, which is why the
                // officer cut carries a rank requirement instead of being listed per post.
                Assert.That(master.GetGear(JumpsuitSlot), Is.EqualTo("EmberClothingUniformUtilityExpeditionaryOfficer"),
                    "A commissioned officer should be in the officer's cut.");

                // The half that is easy to lose: whatever the service put on them, the post's own
                // kit has to survive underneath it.
                Assert.That(corps.GetGear("belt"), Is.EqualTo(contractor.GetGear("belt")),
                    "The engineer's belt should not depend on who employs the engineer.");
                Assert.That(corps.GetGear("belt"), Is.Not.Empty,
                    "The engineer lost the toolbelt the vanilla kit had.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// What this character in this post would actually be issued, resolved the same way the spawn
    /// and the lobby preview both resolve it.
    /// </summary>
    private static StartingGearPrototype Kit(
        IPrototypeManager protoMan,
        SharedStationSpawningSystem spawning,
        JobPrototype job,
        string branch,
        string rank)
    {
        Assert.That(job.StartingGear, Is.Not.Null, $"{job.ID} issues no kit at all.");

        var profile = new HumanoidCharacterProfile { Branch = branch, Rank = rank };
        return spawning.ApplySubGear(protoMan.Index<StartingGearPrototype>(job.StartingGear!), profile, job);
    }
}
