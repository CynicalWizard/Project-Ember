using Content.Shared.Ember.Walls;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Structures;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberMaterialTintComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmberWallMaterialPrototype> Material;

    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public float Alpha = 1f;
}
