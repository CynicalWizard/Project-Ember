using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// The category of attachment point an accessory occupies on a piece of clothing.
/// A holder only accepts categories listed in its ValidSlots, and can be told to allow
/// only one accessory per category via its RestrictedSlots.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's ACCESSORY_SLOT_* defines in code/__defines/items_clothing.dm.
/// </remarks>
[Serializable, NetSerializable]
public enum EmberAccessorySlot : byte
{
    /// <summary>Ties, scarves, pins - anything purely cosmetic. Bay: ACCESSORY_SLOT_DECOR.</summary>
    Decor,

    /// <summary>Pouches and webbing. Bay: ACCESSORY_SLOT_UTILITY.</summary>
    Utility,

    /// <summary>Holsters. Bay: ACCESSORY_SLOT_HOLSTER.</summary>
    Holster,

    /// <summary>Department armbands. Bay: ACCESSORY_SLOT_ARMBAND.</summary>
    Armband,

    /// <summary>Rank insignia. Bay: ACCESSORY_SLOT_RANK.</summary>
    Rank,

    /// <summary>Flash-protection attachments. Bay: ACCESSORY_SLOT_FLASH.</summary>
    Flash,

    /// <summary>Medals and awards. Bay: ACCESSORY_SLOT_MEDAL.</summary>
    Medal,

    /// <summary>Unit insignia. Bay: ACCESSORY_SLOT_INSIGNIA.</summary>
    Insignia,

    /// <summary>Unit insignia sized for a voidsuit. Bay: ACCESSORY_SLOT_INSIGNIA_EVA.</summary>
    InsigniaEva,

    /// <summary>Chest armour plate. Bay: ACCESSORY_SLOT_ARMOR_CHEST.</summary>
    ArmorChest,

    /// <summary>Arm guards. Bay: ACCESSORY_SLOT_ARMOR_ARMS.</summary>
    ArmorArms,

    /// <summary>Leg guards. Bay: ACCESSORY_SLOT_ARMOR_LEGS.</summary>
    ArmorLegs,

    /// <summary>Pouches bolted to a plate carrier. Bay: ACCESSORY_SLOT_ARMOR_STORAGE.</summary>
    ArmorStorage,

    /// <summary>Everything else that bolts to armour. Bay: ACCESSORY_SLOT_ARMOR_MISC.</summary>
    ArmorMisc,

    /// <summary>Helmet covers. Bay: ACCESSORY_SLOT_HELMET_COVER.</summary>
    HelmetCover,

    /// <summary>Helmet decorations. Bay: ACCESSORY_SLOT_HELMET_DECOR.</summary>
    HelmetDecor,

    /// <summary>Helmet visors. Bay: ACCESSORY_SLOT_HELMET_VISOR.</summary>
    HelmetVisor,

    /// <summary>Vision modules for eyewear. Bay: ACCESSORY_SLOT_GLASSES_VISION.</summary>
    GlassesVision,

    /// <summary>HUD modules for eyewear. Bay: ACCESSORY_SLOT_GLASSES_HUD.</summary>
    GlassesHud,
}

/// <summary>
/// Per-accessory behaviour switches.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's ACCESSORY_REMOVABLE / ACCESSORY_HIDDEN / ACCESSORY_HIGH_VISIBILITY.
/// </remarks>
[Flags, Serializable, NetSerializable]
public enum EmberAccessoryFlags : byte
{
    None = 0,

    /// <summary>The accessory can be taken back off by hand. Bay: ACCESSORY_REMOVABLE.</summary>
    Removable = 1 << 0,

    /// <summary>The accessory is never mentioned when the clothing is examined. Bay: ACCESSORY_HIDDEN.</summary>
    Hidden = 1 << 1,

    /// <summary>
    /// The accessory is obvious enough to be named on the wearer's own examine line,
    /// rather than only when the clothing itself is examined. Bay: ACCESSORY_HIGH_VISIBILITY.
    /// </summary>
    HighVisibility = 1 << 2,

    /// <summary>Bay: ACCESSORY_DEFAULT_FLAGS.</summary>
    Default = Removable,
}

/// <summary>
/// Which cut of department patch a garment takes. The patch is sewn to a different place on each
/// kind of garment and is drawn as a different sprite, so the garment is what knows this - the
/// character only knows which department they are in.
/// </summary>
/// <remarks>
/// Bay has no equivalent because Bay does not resolve this at all: every uniform prototype names
/// the finished patch in its own <c>accessories</c> list, which is why eight departments times
/// three cuts is sixty-two prototypes there. Naming the cut instead lets one department own one
/// entry and the garment supply the rest.
/// </remarks>
[Serializable, NetSerializable]
public enum EmberInsigniaCut : byte
{
    /// <summary>Working dress. Bay's <c>dept_exped</c>.</summary>
    Utility,

    /// <summary>Service uniform and service jacket. Bay's <c>dept_exped_service</c>.</summary>
    Service,

    /// <summary>Fleet, which draws one patch for everything. Bay's <c>dept_fleet</c>.</summary>
    Fleet,
}
