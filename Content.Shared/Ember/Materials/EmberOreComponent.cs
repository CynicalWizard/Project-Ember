using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

[RegisterComponent]
public sealed partial class EmberOreComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmberMaterialPrototype> Material;
}
