using Content.Shared.Ember.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Furniture;

/// <summary>
/// A chair, bed or sofa drawn out of a frame and its upholstery rather than as one picture.
/// </summary>
/// <remarks>
/// Bay draws each of these in parts, and tints each part from a different material: the frame takes the wood
/// or steel it was built from, the padding takes whatever was used to upholster it. One drawing therefore
/// serves a steel chair, a wooden one and a black leather one, which is why Bay can afford a dozen chairs on
/// two sheets.
///
/// Some of those parts belong above whoever is sitting in the chair — the back, and the arms that close around
/// them — and a sprite has one draw depth for all its layers. So the parts that go over are drawn by a second
/// entity that rides along at <see cref="Content.Shared.DrawDepth.DrawDepth.OverMobs"/>, and this same
/// component sits on it with <see cref="DrawsOver"/> set. See EmberProceduralFurnitureSystem on both sides.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralFurnitureComponent : Component
{
    /// <summary>
    /// The name the parts are built from: a chair called <c>comfychair</c> is drawn out of <c>comfychair</c>,
    /// <c>comfychair_over</c>, <c>comfychair_padding</c> and so on.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string BaseState = default!;

    [DataField, AutoNetworkedField]
    public ResPath Sprite = new("/Textures/Ember/Structures/Furniture/furniture.rsi");

    /// <summary>What it is built out of, which is what colours the frame.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype> Material = "Steel";

    /// <summary>What it is upholstered with, if anything. A bare frame is a perfectly good chair.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype>? Padding;

    /// <summary>
    /// Whether to draw the <c>_special</c> part: trim that belongs to the design rather than to the material.
    /// Bay draws it above the sitter, the same as the back of the chair.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Special;

    /// <summary>
    /// Whether that trim takes the frame's colour. The gold on the captain's chair does not — it is gold —
    /// but a shuttle seat's harness is part of the seat and is painted with it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SpecialTinted;

    /// <summary>
    /// Whether the trim survives someone sitting down. A shuttle seat's is the harness hanging open, so it
    /// goes the moment anyone is strapped in; the captain's gold stays where it is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SpecialWhenOccupied = true;

    /// <summary>
    /// A different set of parts to draw while someone is sitting here, if the design has one.
    /// </summary>
    /// <remarks>
    /// The shuttle seat is the only one on Bay that does this: <c>post_buckle_mob</c> swaps its whole base
    /// icon from <c>shuttle_chair</c> to <c>shuttle_chair-b</c>, which is why the second one has arms — the
    /// harness closed around its occupant — and the first has none.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public string? OccupiedState;

    /// <summary>
    /// Set on the companion entity: draw the parts that belong above the sitter instead of below them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DrawsOver;

    /// <summary>
    /// Whether anyone is sitting here. The arms only close around an occupant, so they are drawn only then.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Occupied;

    /// <summary>The companion that draws the parts above the sitter. Server-side bookkeeping.</summary>
    [DataField]
    public EntProtoId OverlayPrototype = "EmberFurnitureOver";

    [ViewVariables]
    public EntityUid? Overlay;
}
