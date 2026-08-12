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

    /// <summary>
    /// Memo key for the layers drawn on the clothing item itself. Cannot collide with a slot name.
    /// </summary>
    private const string ItemVisualsKey = "$item";

    /// <summary>
    /// Stands in for "this accessory draws nothing here", so a negative result is cached too.
    /// </summary>
    private static readonly List<PrototypeLayerData> NoLayers = new();

    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EmberAccessorySystem _accessory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>
    /// Layers already resolved for an accessory, keyed by holder slot (species-qualified where the
    /// wearer has a species).
    /// </summary>
    /// <remarks>
    /// This exists for one reason: <see cref="OnGetVisuals"/> runs once per worn slot on every
    /// redraw of the wearer, and every AppearanceChangeEvent redraws every slot. Without the memo,
    /// the RSI fallback allocates a fresh list and layer on each of those, for every attached
    /// accessory - which is exactly the path a chest full of medals takes.
    /// </remarks>
    private readonly Dictionary<EntityUid, Dictionary<string, List<PrototypeLayerData>>> _resolved = new();

    /// <summary>
    /// Layer keys this system has added to each holder's own sprite, so they can be taken off again.
    /// </summary>
    private readonly Dictionary<EntityUid, List<string>> _itemLayers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetVisuals,
            after: [typeof(ClientClothingSystem)]);

        SubscribeLocalEvent<EmberAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnAccessoryInserted);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnAccessoryRemoved);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, ComponentStartup>(OnHolderStartup);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, ComponentShutdown>(OnHolderShutdown);

        SubscribeLocalEvent<EmberAccessoryComponent, ComponentShutdown>(OnAccessoryShutdown);
    }

    /// <summary>
    /// Drops an accessory's memo. Call this if anything ever changes an accessory's sprite or
    /// <see cref="EmberAccessoryComponent.EquippedState"/> at runtime - a chameleon accessory, say.
    /// Nothing does today, which is the only reason the memo can live for the entity's lifetime.
    /// </summary>
    public void InvalidateVisuals(EntityUid accessory)
    {
        _resolved.Remove(accessory);
    }

    private void OnAccessoryShutdown(EntityUid uid, EmberAccessoryComponent component, ComponentShutdown args)
    {
        _resolved.Remove(uid);
    }

    /// <summary>
    /// Catches clothing that already had accessories on it before the client ever saw it - a
    /// uniform pulled out of a locker, say.
    /// </summary>
    private void OnHolderStartup(EntityUid uid, EmberAccessoryHolderComponent component, ComponentStartup args)
    {
        UpdateItemSprite(uid, component);
    }

    private void OnHolderShutdown(EntityUid uid, EmberAccessoryHolderComponent component, ComponentShutdown args)
    {
        _itemLayers.Remove(uid);
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

        // Redraws the accessory on the wearer...
        _item.VisualsChanged(uid);

        // ...and on the garment's own icon, which is what the inventory slot shows.
        UpdateItemSprite(uid, component);
    }

    /// <summary>
    /// Rebuilds the accessory layers on the clothing item's own sprite.
    /// </summary>
    /// <remarks>
    /// Bay equivalent: on_attached()/on_removed() adding and cutting get_inv_overlay() on the
    /// parent. Without this an attached accessory shows on the wearer but the garment's icon in
    /// the inventory slot, in hand and on the floor stays bare.
    /// </remarks>
    private void UpdateItemSprite(EntityUid uid, EmberAccessoryHolderComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (_itemLayers.TryGetValue(uid, out var keys))
        {
            foreach (var key in keys)
            {
                _sprite.RemoveLayer((uid, sprite), key);
            }

            keys.Clear();
        }

        if (!_accessory.TryGetContainer((uid, component), out var container) || container.Count == 0)
            return;

        keys ??= _itemLayers[uid] = new List<string>();

        var i = 0;
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if (!TryGetItemLayers(accessory, comp, out var layers))
                continue;

            foreach (var layerData in layers)
            {
                var key = $"ember-accessory-item-{i}";
                i++;

                var index = _sprite.LayerMapReserve((uid, sprite), key);

                // Authored layers may leave the RSI out, meaning "the accessory's own sprite".
                // Set before LayerSetData, the order ClientClothingSystem uses.
                if (layerData.RsiPath == null
                    && layerData.TexturePath == null
                    && sprite[index] is SpriteComponent.Layer { RSI: null } layer
                    && TryComp<SpriteComponent>(accessory, out var accessorySprite))
                {
                    _sprite.LayerSetRsi(layer, accessorySprite.BaseRSI);
                }

                _sprite.LayerSetData((uid, sprite), index, layerData);
                keys.Add(key);
            }
        }
    }

    /// <summary>
    /// Layers for the garment's own icon: an authored set, else a dedicated state, else the same
    /// onmob sprite used on the wearer.
    /// </summary>
    /// <remarks>
    /// The final fallback is Bay's too - get_inv_overlay() ends up reusing the onmob icon facing
    /// south when no purpose-drawn variant exists. It reads as approximate, because the sprite was
    /// positioned for a body rather than for a folded garment; authoring ItemState fixes that.
    /// </remarks>
    private bool TryGetItemLayers(
        EntityUid uid,
        EmberAccessoryComponent component,
        out List<PrototypeLayerData> layers)
    {
        if (!_resolved.TryGetValue(uid, out var perSlot))
        {
            perSlot = new Dictionary<string, List<PrototypeLayerData>>();
            _resolved[uid] = perSlot;
        }

        if (perSlot.TryGetValue(ItemVisualsKey, out layers!))
            return true;

        var resolved = ResolveItemLayers(uid, component);
        if (resolved == null)
        {
            // Same reasoning as TryGetLayers: a miss here may only mean "not loaded yet".
            layers = NoLayers;
            return false;
        }

        perSlot[ItemVisualsKey] = resolved;
        layers = resolved;
        return true;
    }

    private List<PrototypeLayerData>? ResolveItemLayers(EntityUid uid, EmberAccessoryComponent component)
    {
        if (component.ItemVisuals is { Count: > 0 } authored)
            return authored;

        var state = component.ItemState ?? component.EquippedState;
        if (state == null)
            return null;

        if (!TryComp<SpriteComponent>(uid, out var sprite) || sprite.BaseRSI is not { } rsi)
            return null;

        if (!rsi.TryGetState(state, out _))
            return null;

        return new List<PrototypeLayerData>
        {
            new()
            {
                RsiPath = rsi.Path.ToString(),
                State = state,
            },
        };
    }

    private void OnGetVisuals(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        GetEquipmentVisualsEvent args)
    {
        // The overwhelming majority of worn clothing carries no accessories at all, so this is the
        // branch that has to stay cheap.
        if (!_accessory.TryGetContainer((uid, component), out var container) || container.Count == 0)
            return;

        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;

        // Built once per slot rather than once per accessory - it is the same key for all of them.
        var speciesKey = inventory.SpeciesId == null ? null : $"{args.Slot}-{inventory.SpeciesId}";

        // Bay hides accessories whose body_location is covered by the suit. We have no coverage
        // data, so this is an opt-in flag on the accessory instead - and it is what keeps a chest
        // of medals free to draw while a coat is on over it.
        var coveredByOuter = args.Slot != OuterClothingSlot
            && _inventory.TryGetSlotEntity(args.Equipee, OuterClothingSlot, out _, inventory);

        var i = 0;
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if (coveredByOuter && comp.HideUnderOuterClothing)
                continue;

            if (!TryGetLayers(accessory, comp, args.Slot, speciesKey, inventory.SpeciesId, out var layers))
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
        string? speciesKey,
        string? speciesId,
        out List<PrototypeLayerData> layers)
    {
        // Species-qualified whenever the wearer has a species, even when the resolved sprite turns
        // out to be the generic one: caching a generic result under the bare slot would let it
        // short-circuit the species lookup for a different species later.
        var cacheKey = speciesKey ?? slot;

        if (!_resolved.TryGetValue(uid, out var perSlot))
        {
            perSlot = new Dictionary<string, List<PrototypeLayerData>>();
            _resolved[uid] = perSlot;
        }

        if (perSlot.TryGetValue(cacheKey, out layers!))
            return true;

        var resolved = ResolveLayers(uid, component, slot, speciesKey, speciesId);
        if (resolved == null)
        {
            // Deliberately not remembered. Resolution reads the accessory's own sprite, and the
            // first time this runs can be while the client is still applying state, before that
            // sprite exists - caching the miss would blank the accessory for good.
            layers = NoLayers;
            return false;
        }

        perSlot[cacheKey] = resolved;
        layers = resolved;
        return true;
    }

    private List<PrototypeLayerData>? ResolveLayers(
        EntityUid uid,
        EmberAccessoryComponent component,
        string slot,
        string? speciesKey,
        string? speciesId)
    {
        if (speciesKey != null && component.Visuals.TryGetValue(speciesKey, out var authored))
            return authored;

        if (component.Visuals.TryGetValue(slot, out authored))
            return authored;

        if (component.EquippedState is not { } state)
            return null;

        RSI? rsi = null;

        if (TryComp<ClothingComponent>(uid, out var clothing) && clothing.Sprite != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.Sprite).RSI;
        else if (TryComp(uid, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return null;

        if (speciesId != null && rsi.TryGetState($"{state}-{speciesId}", out _))
            state = $"{state}-{speciesId}";
        else if (!rsi.TryGetState(state, out _))
            return null;

        // Deliberately not a collection expression: the compiler lowers those to
        // CollectionsMarshal.SetCount, which the sandbox rejects.
        return new List<PrototypeLayerData>
        {
            new()
            {
                RsiPath = rsi.Path.ToString(),
                State = state,
            },
        };
    }
}
