using Robust.Shared.Prototypes;
using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Walls;

[Prototype("emberWallMaterial")]
public sealed partial class EmberWallMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField(required: true)]
    public string StateBase { get; set; } = default!;

    [DataField]
    public Color Color { get; set; } = Color.White;

    [DataField]
    public string? SmoothKey;

    [DataField]
    public string? ReinforcementStateBase;

    [DataField]
    public Color? ReinforcementColor;
    
    [DataField]
    public ProtoId<EmberMaterialPrototype>? PhysicalMaterial;
}
