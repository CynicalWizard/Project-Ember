using Robust.Shared.Containers;

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
    /// Categories that may only be filled once. A category in <see cref="ValidSlots"/> but not
    /// here can be attached repeatedly (Bay lets you pile on medals, but only one holster).
    /// Bay: restricted_accessory_slots.
    /// </summary>
    [DataField]
    public List<EmberAccessorySlot> RestrictedSlots = new();

    /// <summary>
    /// Hard cap on attached accessories, as a backstop against unbounded stacking on clothing
    /// with many unrestricted categories.
    /// </summary>
    [DataField]
    public int MaxAccessories = 6;

    [DataField]
    public string ContainerId = "ember_accessories";

    [ViewVariables]
    public Container? Container;
}
