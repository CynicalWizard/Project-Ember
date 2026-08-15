#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Shared._EE.Contractors.Components;
using Content.Shared.Ember.Background;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A passport is issued by the faction axis, and the factions that issue none do so deliberately.
/// </summary>
/// <remarks>
/// Both halves of this fail quietly. A passport naming an entity that does not exist means the
/// spawn path returns early and the character wakes up without papers, which looks exactly like the
/// stateless case working as designed; and a stateless character who is handed a document looks
/// like nothing at all until someone examines it.
/// </remarks>
[TestFixture]
public sealed class EmberPassportIssueTest
{
    /// <summary>
    /// Allegiances that issue nothing, and why. Written out rather than derived, so that giving one
    /// of them a document has to be a decision someone made on purpose.
    /// </summary>
    private static readonly Dictionary<string, string> IssueNothing = new()
    {
        ["EmberFactionStateless"] = "no state claims them, which is what stateless means",
        ["EmberFactionOther"] = "the catch-all names no particular polity",
        ["EmberFactionDionaChorus"] = "the Chorus is not a state and issues nothing",
        ["EmberFactionUnathiIndependent"] = "an independent clan has no polity above it",
    };

    [Test]
    public async Task EveryIssuedPassportResolvesToARealDocument()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        var factions = protoMan.EnumeratePrototypes<EmberBackgroundPrototype>()
            .Where(o => o.Axis == EmberBackgroundAxis.Faction)
            .ToList();

        Assert.That(factions, Is.Not.Empty, "the faction axis has no entries at all");

        await server.WaitAssertion(() =>
        {
            foreach (var faction in factions)
            {
                if (IssueNothing.TryGetValue(faction.ID, out var reason))
                {
                    Assert.That(faction.Passport, Is.Null,
                        $"{faction.ID} issues a passport, but {reason}");
                    continue;
                }

                Assert.That(faction.Passport, Is.Not.Null,
                    $"{faction.ID} issues no passport and is not listed as one of the allegiances "
                    + "that deliberately issues none");

                Assert.That(protoMan.TryIndex(faction.Passport!.Value, out EntityPrototype? proto), Is.True,
                    $"{faction.ID} names passport '{faction.Passport}', which is not an entity prototype");

                // Without the component the spawn path still puts the item in the bag, and it is
                // then a booklet that reports nothing at all when examined.
                var componentName = compFactory.GetComponentName(typeof(PassportComponent));
                Assert.That(proto!.Components.ContainsKey(componentName), Is.True,
                    $"{proto.ID} is issued as a passport but has no PassportComponent");
            }
        });

        await pair.CleanReturnAsync();
    }
}
