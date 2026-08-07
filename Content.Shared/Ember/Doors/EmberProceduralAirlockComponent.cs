using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Doors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralAirlockComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmberAirlockStylePrototype> Style;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool Glass;

    /// <summary>
    /// Whether this is a docking port, and draws the collar a shuttle clamps onto.
    /// </summary>
    /// <remarks>
    /// The collar goes on the edge the door faces, which is the edge a shuttle has to arrive at: docking pairs
    /// two ports whose world rotations point at each other, so the rotation is the mapper's to set and the
    /// player's to read. It is not painted with the door, so the direction stays legible whatever colour
    /// somebody sprays it.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public bool Docking;

    /// <summary>
    /// Whether the door turns its picture to sit square in the walls beside it.
    /// </summary>
    /// <remarks>
    /// Every other door should: mappers leave whatever rotation, and Bay picks the dir from the neighbours
    /// rather than trusting it. A docking port must not, because for that one the rotation is the data —
    /// turning the picture off it would draw a port facing somewhere no shuttle can arrive from.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public bool FacesWalls = true;

    [DataField, AutoNetworkedField]
    public Color? DoorColor;

    [DataField, AutoNetworkedField]
    public Color? StripeColor;

    [DataField, AutoNetworkedField]
    public Color? WindowColor;

    /// <summary>
    /// What colour the docking collar is sprayed, if anyone has bothered. Left alone it is bare clamp metal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? DockingColor;

    [DataField]
    public ResPath DoorSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/door.rsi");

    [DataField]
    public ResPath ColorSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/color.rsi");

    [DataField]
    public ResPath ColorFillSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/fill_color.rsi");

    [DataField]
    public ResPath SteelFillSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/fill_steel.rsi");

    [DataField]
    public ResPath GlassFillSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/fill_glass.rsi");

    [DataField]
    public ResPath DockingSprite = new("/Textures/Ember/Structures/Doors/Airlocks/External/docking.rsi");

    [DataField]
    public ResPath StripeSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/stripe.rsi");

    [DataField]
    public ResPath StripeFillSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/fill_stripe.rsi");

    [DataField]
    public ResPath GreenLightsSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/lights_green.rsi");

    [DataField]
    public ResPath DenyLightsSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/lights_deny.rsi");

    [DataField]
    public ResPath BoltLightsSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/lights_bolts.rsi");

    [DataField]
    public ResPath PanelSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/panel.rsi");

    [DataField]
    public ResPath WeldedSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/welded.rsi");

    [DataField]
    public ResPath EmagSprite = new("/Textures/Ember/Structures/Doors/Airlocks/Station/emag.rsi");
}
