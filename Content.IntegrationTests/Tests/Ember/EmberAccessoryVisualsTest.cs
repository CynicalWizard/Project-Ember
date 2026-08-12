#nullable enable
using System.Linq;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Ember.Clothing;
using Content.Shared.Foldable;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// An accessory is drawn on the wearer by appending layers while the garment it hangs on is being
/// rendered, so it only survives for as long as nothing re-renders that garment behind its back.
/// </summary>
/// <remarks>
/// Folding a uniform ("remove jacket", "unzip labcoat") swaps the garment's equipped prefix, which
/// tears down and rebuilds every layer for that slot. Nothing in the engine knows the accessory
/// layers have to come back, and nothing fails loudly when they do not - the scarf simply vanishes
/// off the sprite until it is taken off and put back on. That is what this pins down.
/// </remarks>
[TestFixture]
public sealed class EmberAccessoryVisualsTest
{
    private const string JumpsuitSlot = "jumpsuit";
    private const string OuterClothingSlot = "outerClothing";
    private const string LayerPrefix = "jumpsuit-ember-accessory-";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EmberAccessoryTestUniform
  parent: [ ClothingUniformBase, ClothingUniformFoldableBase ]
  components:
  - type: Sprite
    sprite: Clothing/Uniforms/Jumpsuit/color.rsi
  - type: Clothing
    sprite: Clothing/Uniforms/Jumpsuit/color.rsi
";

    [Test]
    public async Task AccessoryStaysOnTheSpriteWhenTheGarmentIsFolded()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });

        var server = pair.Server;
        var client = pair.Client;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        var sInventory = sEntMan.System<InventorySystem>();
        var sAccessory = sEntMan.System<EmberAccessorySystem>();
        var sFoldable = sEntMan.System<FoldableSystem>();

        var player = pair.Player!.AttachedEntity!.Value;
        var sPlayer = player;

        EntityUid uniform = default;
        EntityUid scarf = default;

        // Dress the player in a foldable uniform and hang a scarf off it.
        await server.WaitPost(() =>
        {
            var coords = sEntMan.GetComponent<TransformComponent>(sPlayer).Coordinates;

            // Whatever the round start put on them is in the way. The coat has to go too: scarves
            // set hideUnderOuterClothing, so with one on there would be nothing to see either way
            // and the test would pass or fail for the wrong reason.
            sInventory.TryUnequip(sPlayer, JumpsuitSlot, force: true);
            sInventory.TryUnequip(sPlayer, OuterClothingSlot, force: true);

            uniform = sEntMan.SpawnEntity("EmberAccessoryTestUniform", coords);
            Assert.That(sInventory.TryEquip(sPlayer, uniform, JumpsuitSlot, force: true), Is.True,
                "Could not put the test uniform on the player.");

            scarf = sEntMan.SpawnEntity("ClothingNeckScarfStripedRed", coords);

            var holder = sEntMan.GetComponent<EmberAccessoryHolderComponent>(uniform);
            var accessory = sEntMan.GetComponent<EmberAccessoryComponent>(scarf);

            Assert.That(sAccessory.TryAttach((uniform, holder), (scarf, accessory), null), Is.True,
                "Could not attach the scarf to the test uniform.");
        });

        await pair.RunTicksSync(15);

        var cPlayer = cEntMan.GetEntity(sEntMan.GetNetEntity(sPlayer));
        var cUniform = cEntMan.GetEntity(sEntMan.GetNetEntity(uniform));

        // Staged so a failure says which link of the chain broke, rather than just "nothing drawn".
        Assert.Multiple(() =>
        {
            Assert.That(cEntMan.TryGetComponent(cUniform, out EmberAccessoryHolderComponent? _), Is.True,
                "The client never got the holder component.");

            var cAccessorySys = cEntMan.System<EmberAccessorySystem>();
            Assert.That(cAccessorySys.TryGetContainer(cUniform, out var cContainer), Is.True,
                "The client's holder has no accessory container.");
            Assert.That(cContainer?.Count, Is.EqualTo(1),
                "The client's container does not have the scarf in it.");
        });

        // Ask the visuals handler directly, so a failure separates "the handler produces nothing"
        // from "the handler is fine but nobody re-rendered the slot".
        var probe = string.Empty;
        await client.WaitPost(() =>
        {
            var cScarf = cEntMan.GetEntity(sEntMan.GetNetEntity(scarf));

            var hasAccessory = cEntMan.TryGetComponent(cScarf, out EmberAccessoryComponent? cAcc);
            var hasSprite = cEntMan.TryGetComponent(cScarf, out SpriteComponent? cSprite);
            var rsiPath = cSprite?.BaseRSI?.Path.ToString() ?? "(no rsi)";
            var hasState = cSprite?.BaseRSI?.TryGetState("equipped-NECK", out _) ?? false;

            var ev = new GetEquipmentVisualsEvent(cPlayer, JumpsuitSlot);
            cEntMan.EventBus.RaiseLocalEvent(cUniform, ev);

            probe = $"accessoryComp={hasAccessory}, equippedState={cAcc?.EquippedState ?? "(null)"}, "
                + $"sprite={hasSprite}, rsi={rsiPath}, hasEquippedNeck={hasState}, "
                + $"handlerLayers=[{string.Join(", ", ev.Layers.Select(l => l.Item1))}]";
        });

        Assert.That(AccessoryLayerKeys(cEntMan, cPlayer), Is.Not.Empty,
            $"The scarf never reached the wearer's sprite.\nKeys for the slot: {DescribeSlotKeys(cEntMan, cPlayer)}\nProbe: {probe}");

        // Fold the uniform: this is the "remove jacket" verb.
        await server.WaitPost(() =>
        {
            var foldable = sEntMan.GetComponent<FoldableComponent>(uniform);
            Assert.That(sFoldable.TryToggleFold(uniform, foldable), Is.True, "The test uniform would not fold.");
        });

        await pair.RunTicksSync(15);

        Assert.That(AccessoryLayerKeys(cEntMan, cPlayer), Is.Not.Empty,
            $"Folding the garment dropped the accessory off the wearer's sprite. Keys present for the slot: {DescribeSlotKeys(cEntMan, cPlayer)}");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The layer keys the client actually put on the wearer for the uniform slot. ClientClothingSystem
    /// records these as it renders, which is a far more honest answer than probing the sprite.
    /// </summary>
    private static string[] SlotKeys(IEntityManager entMan, EntityUid player)
    {
        if (!entMan.TryGetComponent(player, out InventorySlotsComponent? slots))
            return [];

        return slots.VisualLayerKeys.TryGetValue(JumpsuitSlot, out var keys) ? keys.ToArray() : [];
    }

    private static string[] AccessoryLayerKeys(IEntityManager entMan, EntityUid player)
    {
        return SlotKeys(entMan, player).Where(k => k.StartsWith(LayerPrefix)).ToArray();
    }

    private static string DescribeSlotKeys(IEntityManager entMan, EntityUid player)
    {
        var keys = SlotKeys(entMan, player);
        return keys.Length == 0 ? "(none)" : string.Join(", ", keys);
    }
}
