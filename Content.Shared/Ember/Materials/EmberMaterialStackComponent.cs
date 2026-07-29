using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

[RegisterComponent]
public sealed partial class EmberMaterialStackComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmberMaterialPrototype> Material;

    [DataField]
    public bool Tint = true;
}
