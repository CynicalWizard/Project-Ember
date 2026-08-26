#nullable enable
using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Ember.Clothing;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// An accessory sewn to a uniform moves with the cloth. SierraBay12 draws a department patch three
/// times - flat, with the sleeves up, and with the garment pulled to the waist - and the converted
/// sheets carry all three across.
/// </summary>
/// <remarks>
/// Worth a test of its own because every part of this fails quietly. A missing roll variant does
/// not throw; it draws the flat sprite over a rolled sleeve, which reads as art that is slightly
/// off rather than as a bug. A rank board that should disappear when the uniform comes off the
/// shoulders instead floats on bare skin. And the layer memo is keyed per accessory, so a stale
/// key would pin the first state resolved for the rest of the round.
///
/// Deliberately runs against the real converted content rather than test prototypes: the states
/// being looked for are the ones the conversion produced, so a test with its own sprites would
/// pass over a sheet that was sliced wrong.
/// </remarks>
[TestFixture]
public sealed class EmberAccessoryRollTest
{
    private const string JumpsuitSlot = "jumpsuit";
    private const string OuterClothingSlot = "outerClothing";

    /// <summary>Corps utility uniform. Its RSI has both roll variants.</summary>
    private const string Uniform = "EmberClothingUniformUtilityExpeditionary";

    /// <summary>A department patch, whose sheet carries rolled- and down- states.</summary>
    private const string Patch = "EmberClothingAccessoryDeptCommand";

    /// <summary>An E-1 rank board, whose sheet carries neither.</summary>
    private const string Rank = "EmberClothingAccessoryEcrankE1";

    [Test]
    public async Task AccessoriesFollowTheGarmentsRoll()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });

        var server = pair.Server;
        var client = pair.Client;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        var sInventory = sEntMan.System<InventorySystem>();
        var sAccessory = sEntMan.System<EmberAccessorySystem>();
        var sRoll = sEntMan.System<SharedEmberRollableClothingSystem>();

        var sPlayer = pair.Player!.AttachedEntity!.Value;

        EntityUid uniform = default;

        await server.WaitPost(() =>
        {
            var coords = sEntMan.GetComponent<TransformComponent>(sPlayer).Coordinates;

            // The round-start kit is in the way, and a coat over the uniform would hide the
            // accessories for a reason that has nothing to do with what is being measured.
            sInventory.TryUnequip(sPlayer, JumpsuitSlot, force: true);
            sInventory.TryUnequip(sPlayer, OuterClothingSlot, force: true);

            uniform = sEntMan.SpawnEntity(Uniform, coords);
            Assert.That(sInventory.TryEquip(sPlayer, uniform, JumpsuitSlot, force: true), Is.True,
                "Could not put the expeditionary uniform on the player.");

            var holder = sEntMan.GetComponent<EmberAccessoryHolderComponent>(uniform);

            foreach (var proto in new[] { Patch, Rank })
            {
                var accessory = sEntMan.SpawnEntity(proto, coords);
                Assert.That(
                    sAccessory.TryAttach(
                        (uniform, holder),
                        (accessory, sEntMan.GetComponent<EmberAccessoryComponent>(accessory)),
                        null),
                    Is.True,
                    $"Could not attach {proto} to the uniform.");
            }
        });

        await pair.RunTicksSync(15);

        var cPlayer = cEntMan.GetEntity(sEntMan.GetNetEntity(sPlayer));
        var cUniform = cEntMan.GetEntity(sEntMan.GetNetEntity(uniform));

        var flat = await AccessoryStates(client, cEntMan, cUniform, cPlayer);

        Assert.That(flat, Is.EquivalentTo(new[] { "equipped-ACCESSORY", "equipped-ACCESSORY" }),
            "Neither accessory drew its plain state on an unrolled uniform.");

        // Sleeves up. Both sheets are asked for a rolled variant; only the patch has one, and the
        // board keeps its own sprite rather than disappearing, because rolled sleeves change the
        // forearms and leave the chest alone.
        await SetRoll(server, sEntMan, sRoll, uniform, EmberClothingRoll.Sleeves);
        await pair.RunTicksSync(15);

        var sleeves = await AccessoryStates(client, cEntMan, cUniform, cPlayer);

        Assert.That(sleeves, Is.EquivalentTo(new[] { "rolled-equipped-ACCESSORY", "equipped-ACCESSORY" }),
            "Rolling the sleeves did not move the patch to its rolled sprite, or dropped the rank board.");

        // Asking a rolled-sleeve garment to come down lands on flat, not on the waist: Bay never
        // lets the two states coexist, so the first pull is the one that puts the sleeves back.
        // Asserted here rather than worked around, because it is the reason the next step needs
        // two calls and would otherwise look like a mistake.
        await SetRoll(server, sEntMan, sRoll, uniform, EmberClothingRoll.Down);
        await pair.RunTicksSync(15);

        Assert.That(await AccessoryStates(client, cEntMan, cUniform, cPlayer),
            Is.EquivalentTo(new[] { "equipped-ACCESSORY", "equipped-ACCESSORY" }),
            "Pulling down a garment with its sleeves rolled should unroll the sleeves first.");

        // Pulled to the waist. The patch has art for it; the board has none and the chest it sat
        // on is now bare, so it is not drawn at all.
        await SetRoll(server, sEntMan, sRoll, uniform, EmberClothingRoll.Down);
        await pair.RunTicksSync(15);

        var down = await AccessoryStates(client, cEntMan, cUniform, cPlayer);

        Assert.That(down, Is.EquivalentTo(new[] { "down-equipped-ACCESSORY" }),
            "A uniform open to the waist should draw the patch's own down sprite and nothing else.");

        // Back to flat, which is also the check that the per-roll layer memo is not stale.
        await SetRoll(server, sEntMan, sRoll, uniform, EmberClothingRoll.None);
        await pair.RunTicksSync(15);

        Assert.That(await AccessoryStates(client, cEntMan, cUniform, cPlayer),
            Is.EquivalentTo(new[] { "equipped-ACCESSORY", "equipped-ACCESSORY" }),
            "Unrolling the uniform left the accessories on their rolled sprites.");

        await pair.CleanReturnAsync();
    }

    private static async Task SetRoll(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        IEntityManager entMan,
        SharedEmberRollableClothingSystem system,
        EntityUid uniform,
        EmberClothingRoll roll)
    {
        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<EmberRollableClothingComponent>(uniform);
            Assert.That(system.TrySetRoll((uniform, comp), roll), Is.True,
                $"The uniform refused to go to {roll}.");
        });
    }

    /// <summary>
    /// The RSI states the client would draw for the uniform's accessories right now.
    /// </summary>
    /// <remarks>
    /// Raised directly rather than read back off the wearer's sprite, because the layer keys the
    /// wearer records say only that something was drawn. The state name is the whole question here.
    /// </remarks>
    private static async Task<string[]> AccessoryStates(
        Robust.UnitTesting.RobustIntegrationTest.ClientIntegrationInstance client,
        IEntityManager entMan,
        EntityUid uniform,
        EntityUid player)
    {
        var states = System.Array.Empty<string>();

        await client.WaitPost(() =>
        {
            var ev = new GetEquipmentVisualsEvent(player, JumpsuitSlot);
            entMan.EventBus.RaiseLocalEvent(uniform, ev);

            states = ev.Layers
                .Where(l => l.Item1.Contains("ember-accessory"))
                .Select(l => l.Item2.State ?? "(no state)")
                .ToArray();
        });

        return states;
    }
}
