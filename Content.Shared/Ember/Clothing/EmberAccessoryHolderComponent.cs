namespace Content.Shared.Ember.Clothing;

/// <summary>
/// Marks a piece of clothing as able to carry accessories. Accessories live in a container on
/// this entity, so they follow it into storage, are stripped with it, and are deleted with it.
/// </summary>
/// <remarks>
/// Ported from the valid_accessory_slots / restricted_accessory_slots / accessories vars on
/// SierraBay12's /obj/item/clothing (code/modules/clothing/clothing_accessories.dm).
/// An accessory may itself be a holder - that is how Bay hangs pouches off a webbing rig.
/// </remarks>
[RegisterComponent]
public sealed partial class EmberAccessoryHolderComponent : Component
{
    /// <summary>
    /// Categories of accessory this clothing accepts at all. Empty means it takes none.
    /// Bay: valid_accessory_slots.
    /// </summary>
    [DataField]
    public List<EmberAccessorySlot> ValidSlots = new();

    /// <summary>
    /// How many accessories of a given category may be attached at once. Categories missing from
    /// this map use <see cref="DefaultSlotLimit"/>.
    /// </summary>
    /// <remarks>
    /// Generalises Bay's restricted_accessory_slots, which is only ever "one" or "unlimited".
    /// Unlimited is not a useful option in practice - it lets you hang a dozen scarves off one
    /// shirt - so the numeric limit replaces it outright, and Bay's restricted categories are
    /// simply the ones left at the default of one.
    /// </remarks>
    [DataField]
    public Dictionary<EmberAccessorySlot, int> SlotLimits = new();

    /// <summary>
    /// Limit applied to any category not named in <see cref="SlotLimits"/>.
    /// </summary>
    [DataField]
    public int DefaultSlotLimit = 1;

    /// <summary>
    /// Hard cap across all categories, as a backstop for clothing that allows many of them.
    /// </summary>
    [DataField]
    public int MaxAccessories = 6;

    [DataField]
    public string ContainerId = "ember_accessories";
}
