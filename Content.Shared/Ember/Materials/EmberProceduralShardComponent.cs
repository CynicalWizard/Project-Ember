using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Materials;

/// <summary>
/// Debris that takes its name, colour and sprite from the material it broke off. Bay has one shard type that is
/// handed a material when it is created, and this is the same idea: one prototype covers every material rather
/// than one prototype per material.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralShardComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype> Material;

    /// <summary>
    /// Which of the three sizes this piece drew. Rolled on the server so both sides show the same one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Size;

    [DataField]
    public ResPath Sprite = new("/Textures/Ember/Objects/Materials/shards.rsi");
}
