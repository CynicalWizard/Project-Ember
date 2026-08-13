using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// A uniform that can be worn with its sleeves rolled up or pulled down to the waist.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's <c>/obj/item/clothing/under</c>, which carries <c>rolled_sleeves</c>
/// and <c>rolled_down</c> as two independent flags. They are independent in the data and are
/// <em>not</em> independent in play — Bay refuses to roll sleeves on a garment already pulled
/// down, and pulling down clears rolled sleeves. So there are three states, not four, and one
/// enum says that better than two booleans that must never both be set.
///
/// <see cref="Content.Shared.Clothing.Components.FoldableClothingComponent"/> already does a
/// two-state version of this and is not enough here: it is a boolean, and its prefix lives in
/// <see cref="Content.Shared.Clothing.Components.ClothingComponent.EquippedPrefix"/>, which holds
/// one string. This needs to compose with the female sprite variant, so the roll keeps its state
/// here and the client builds the sprite name from both.
///
/// Bay decides rollability by looking for the sprite state at runtime. We ask the data instead.
/// The auto-detection saved Bay from writing a flag on several hundred garments; ours are
/// generated from the same source, so the flag costs nothing and a missing sprite becomes a
/// visible mistake rather than a silently missing verb.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberRollableClothingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EmberClothingRoll Roll = EmberClothingRoll.None;

    /// <summary>
    /// Whether the sprite has a sleeves-rolled variant. A skirt does not.
    /// </summary>
    [DataField]
    public bool CanRollSleeves = true;

    /// <summary>
    /// Whether the sprite has a pulled-down variant.
    /// </summary>
    [DataField]
    public bool CanRollDown = true;
}

[Serializable, NetSerializable]
public enum EmberClothingRoll : byte
{
    None,

    /// <summary>Sleeves up. Bay's <c>_r_s</c>.</summary>
    Sleeves,

    /// <summary>Pulled down to the waist. Bay's <c>_d_s</c>.</summary>
    Down,
}
