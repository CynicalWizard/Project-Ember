using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

[RegisterComponent]
public sealed partial class EmberMaterialFloorComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmberMaterialPrototype> Material;

    [DataField]
    public bool Tint = true;

    [DataField]
    public bool RenameEntity = true;
}
