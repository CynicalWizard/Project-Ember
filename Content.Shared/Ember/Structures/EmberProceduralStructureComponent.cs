using Content.Shared.Ember.Walls;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Structures;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralStructureComponent : Component
{
    public (EntityUid?, Vector2i)? LastPosition;

    public int UpdateGeneration;

    [DataField(required: true)]
    public EmberProceduralStructureRole Role;

    [DataField(required: true)]
    public ProtoId<EmberWallMaterialPrototype> Material;

    [DataField(required: true)]
    public string StateBase = default!;

    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public float Alpha = 1f;

    [DataField]
    public ResPath Sprite = new("/Textures/Ember/Structures/WallFrames/wall_frame_offbay.rsi");

    [DataField]
    public bool Broken;

    [DataField]
    public string BrokenState = "broken";

    [DataField]
    public string BrokenOnFrameState = "broken_onframe";
}

public enum EmberProceduralStructureRole : byte
{
    WallFrame,
    Grille,
    Window,
}
