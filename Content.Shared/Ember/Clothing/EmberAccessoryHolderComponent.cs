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

    /// <summary>
    /// Which cut of department patch this garment takes, when one is issued to it. Only read for
    /// garments that accept <see cref="EmberAccessorySlot.Flash"/> at all.
    /// </summary>
    /// <remarks>
    /// Left on the garment rather than worked out from the wearer because the wearer does not know
    /// it: a Corps rating in a service jacket over a utility uniform is owed both cuts at once, and
    /// which one goes where is a fact about the two garments.
    /// </remarks>
    [DataField]
    public EmberInsigniaCut InsigniaCut = EmberInsigniaCut.Utility;

    /// <remarks>
    /// The container is made on the first attach, not at init, so clothing that never carries an
    /// accessory does not gain one - see EmberAccessorySystem.TryAttach. A prototype that wants to
    /// spawn with accessories already on it should attach them at map init rather than pre-filling
    /// this container: attaching goes through CanAttach, so slot categories and limits are actually
    /// enforced. If you do reach for ContainerFill instead, that prototype must also declare the
    /// container itself, since ContainerFill only fills containers that already exist.
    /// </remarks>
    [DataField]
    public string ContainerId = "ember_accessories";
}
