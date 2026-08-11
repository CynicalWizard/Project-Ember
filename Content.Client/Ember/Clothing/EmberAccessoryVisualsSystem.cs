using System.Diagnostics.CodeAnalysis;
using Content.Client.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Ember.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Ember.Clothing;

/// <summary>
/// Draws accessories on the wearer, on top of the clothing they are attached to.
/// </summary>
/// <remarks>
/// Ported from the way SierraBay12 sums accessory overlays inside
/// /obj/item/clothing/get_mob_overlay(). SS14 already raises
/// <see cref="GetEquipmentVisualsEvent"/> on the equipped item and lets any component on it
/// contribute layers, so the holder simply appends a layer per attached accessory. Running after
/// <see cref="ClientClothingSystem"/> keeps the accessory above the clothing it sits on.
/// </remarks>
public sealed class EmberAccessoryVisualsSystem : EntitySystem
{
    private const string OuterClothingSlot = "outerClothing";

    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetVisuals,
            after: [typeof(ClientClothingSystem)]);

        SubscribeLocalEvent<EmberAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnAccessoryInserted);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnAccessoryRemoved);
    }

    private void OnAccessoryInserted(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        EntInsertedIntoContainerMessage args)
    {
        OnContainerChanged(uid, component, args);
    }

    private void OnAccessoryRemoved(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        EntRemovedFromContainerMessage args)
    {
        OnContainerChanged(uid, component, args);
    }

    /// <summary>
    /// The container only changes on the client once the server's state arrives, so a redraw has to
    /// be kicked off from here as well as from the attach itself.
    /// </summary>
    private void OnContainerChanged(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.ContainerId)
            return;

        _item.VisualsChanged(uid);
    }

    private void OnGetVisuals(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        GetEquipmentVisualsEvent args)
    {
        if (component.Container is not { Count: > 0 } container)
            return;

        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;

        // Bay hides accessories whose body_location is covered by the suit. We have no coverage
        // data, so this is an opt-in flag on the accessory instead.
        var coveredByOuter = args.Slot != OuterClothingSlot
            && _inventory.TryGetSlotEntity(args.Equipee, OuterClothingSlot, out _, inventory);

        var i = 0;
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if (coveredByOuter && comp.HideUnderOuterClothing)
                continue;

            if (!TryGetLayers(accessory, comp, args.Slot, inventory.SpeciesId, out var layers))
                continue;

            foreach (var layer in layers)
            {
                args.Layers.Add(($"{args.Slot}-ember-accessory-{i}", layer));
                i++;
            }
        }
    }

    /// <summary>
    /// Picks the layers for this accessory in this holder slot, preferring an explicit entry in
    /// <see cref="EmberAccessoryComponent.Visuals"/> and falling back to the accessory's own RSI.
    /// </summary>
    /// <remarks>
    /// Bay equivalent: the accessory_icons[slot] / sprite_sheets[bodytype] lookup in
    /// /obj/item/clothing/accessory/get_mob_overlay().
    /// </remarks>
    private bool TryGetLayers(
        EntityUid uid,
        EmberAccessoryComponent component,
        string slot,
        string? speciesId,
        [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        if (speciesId != null && component.Visuals.TryGetValue($"{slot}-{speciesId}", out layers))
            return true;

        if (component.Visuals.TryGetValue(slot, out layers))
            return true;

        layers = null;

        if (component.EquippedState is not { } state)
            return false;

        RSI? rsi = null;

        if (TryComp<ClothingComponent>(uid, out var clothing) && clothing.Sprite != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.Sprite).RSI;
        else if (TryComp(uid, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return false;

        if (speciesId != null && rsi.TryGetState($"{state}-{speciesId}", out _))
            state = $"{state}-{speciesId}";
        else if (!rsi.TryGetState(state, out _))
            return false;

        // Deliberately not a collection expression: the compiler lowers those to
        // CollectionsMarshal.SetCount, which the sandbox rejects.
        layers = new List<PrototypeLayerData>
        {
            new()
            {
                RsiPath = rsi.Path.ToString(),
                State = state,
            },
        };

        return true;
    }
}
