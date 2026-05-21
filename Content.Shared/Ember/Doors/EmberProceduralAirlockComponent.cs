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

    [DataField, AutoNetworkedField]
    public Color? DoorColor;

    [DataField, AutoNetworkedField]
    public Color? StripeColor;

    [DataField, AutoNetworkedField]
    public Color? WindowColor;

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
