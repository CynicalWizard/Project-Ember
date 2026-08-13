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
    /// Explicit sprite layers drawn on the clothing item's own icon - what you see in the
    /// inventory slot, in hand and on the floor. Falls back to <see cref="ItemState"/>, and then
    /// to <see cref="EquippedState"/>.
    /// </summary>
    /// <remarks>
    /// Bay: get_inv_overlay(), which prefers a purpose-drawn "[state]_tie" icon and otherwise
    /// reuses the onmob sprite facing south. Authoring a dedicated state is the tidy option -
    /// the onmob sprite is positioned for a body, not for a folded garment icon.
    /// </remarks>
    [DataField]
    public List<PrototypeLayerData>? ItemVisuals;

    /// <summary>
    /// RSI state drawn on the clothing item's own icon. Bay: the "[state]_tie" icon variant.
    /// </summary>
    [DataField]
    public string? ItemState;

    /// <summary>
    /// Whether the accessory keeps being drawn on a garment pulled down to the waist, when it has
    /// no purpose-drawn variant for that state.
    /// </summary>
    /// <remarks>
    /// Bay writes this per accessory as <c>on_rolled_down</c>, whose three values are "use the
    /// base sprite", "use this other sprite" and "draw nothing". Two of those we get from the art
    /// itself: the converted sheets carry <c>rolled-</c> and <c>down-</c> states wherever Bay drew
    /// one, and the resolver prefers them. What is left over is the case Bay's default gets wrong
    /// - a chest patch on a torso that is now bare skin - so the default here is to hide, and this
    /// field is for the accessories that sit below the fold. A holster is on the belt and a scarf
    /// is round the neck; neither goes anywhere when the shoulders come out of the uniform.
    ///
    /// Rolled sleeves need no equivalent. They change the forearms and nothing else, so an
    /// accessory with no rolled variant simply keeps its own sprite.
    /// </remarks>
    [DataField]
    public bool VisibleWhenRolledDown;

    /// <summary>
    /// Tint applied to the accessory on the wearer only, leaving the item's own icon alone. Null
    /// means the sprite's own colour is used for both, which is what a patch dyed one colour wants.
    /// </summary>
    /// <remarks>
    /// Bay: badgecolor, a second colour var that get_mob_overlay() applies and get_inv_overlay()
    /// does not. It exists for the qualification badges, where the item in your hand is a
    /// purpose-drawn icon per speciality and the thing on your chest is one grey badge that gets
    /// the speciality's colour - the author's note being that they were not going to put nine
    /// thousand coloured pixels in a sprite sheet. Tinting both would multiply a colour into art
    /// that already has one.
    /// </remarks>
    [DataField]
    public Color? EquippedColor;

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
