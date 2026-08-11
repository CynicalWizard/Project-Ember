using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// Marks an item as an accessory: something that is attached to a piece of clothing rather than
/// worn in an inventory slot of its own. Ties, armbands, scarves, holsters, armour plates.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's /obj/item/clothing/accessory
/// (code/modules/clothing/under/accessories/_accessory.dm).
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class EmberAccessoryComponent : Component
{
    /// <summary>
    /// Which category of attachment point this occupies. Bay: the accessory's "slot" var.
    /// </summary>
    [DataField]
    public EmberAccessorySlot Slot = EmberAccessorySlot.Decor;

    /// <summary>
    /// Bay: accessory_flags.
    /// </summary>
    [DataField]
    public EmberAccessoryFlags Flags = EmberAccessoryFlags.Default;

    /// <summary>
    /// Explicit sprite layers drawn on the wearer, keyed by the inventory slot the *holder* is
    /// worn in ("jumpsuit", "outerClothing", ...), optionally suffixed with a species id
    /// ("jumpsuit-Unathi"), exactly like <see cref="Content.Shared.Clothing.Components.ClothingComponent.ClothingVisuals"/>.
    /// When no key matches, <see cref="EquippedState"/> is used instead.
    /// </summary>
    /// <remarks>
    /// Bay: accessory_icons, which maps the host's slot to a different onmob sprite sheet,
    /// plus sprite_sheets for per-species overrides.
    /// </remarks>
    [DataField]
    public Dictionary<string, List<PrototypeLayerData>> Visuals = new();

    /// <summary>
    /// Fallback RSI state drawn on the wearer when nothing in <see cref="Visuals"/> matches.
    /// Resolved against the accessory's own sprite, so most accessories only need this one field.
    /// </summary>
    /// <remarks>
    /// Bay: overlay_state, falling back to icon_state.
    /// </remarks>
    [DataField]
    public string? EquippedState;

    /// <summary>
    /// If true, the accessory stops being drawn on the wearer while something is worn in the
    /// outerClothing slot over the holder.
    /// </summary>
    /// <remarks>
    /// Bay achieves this with body_location and get_visible_accessories(), which we do not have
    /// an equivalent for, since SS14 clothing does not track body_parts_covered.
    /// </remarks>
    [DataField]
    public bool HideUnderOuterClothing;
}
