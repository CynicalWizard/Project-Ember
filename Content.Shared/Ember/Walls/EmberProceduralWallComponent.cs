using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Walls;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralWallComponent : Component
{
    public (EntityUid?, Vector2i)? LastPosition;

    public int UpdateGeneration;

    [DataField(required: true)]
    public ProtoId<EmberWallMaterialPrototype> Material;

    [DataField, AutoNetworkedField]
    public Color? PaintColor;

    [DataField, AutoNetworkedField]
    public Color? StripeColor;

    [DataField, AutoNetworkedField]
    public bool Reinforced;

    [DataField]
    public ResPath Sprite = new("/Textures/Ember/Structures/Walls/wall_masks_offbay.rsi");
}
