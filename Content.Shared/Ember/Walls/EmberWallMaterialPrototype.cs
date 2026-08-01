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

    /// <summary>
    /// Overrides the physical material's <see cref="EmberMaterialPrototype.WallBlendIcons"/>. Only needed for
    /// wall materials that have no physical material to inherit from.
    /// </summary>
    [DataField]
    public Dictionary<string, bool>? BlendKeys;

    /// <summary>
    /// Overrides the physical material's <see cref="EmberMaterialPrototype.WallHasEdges"/>.
    /// </summary>
    [DataField]
    public bool? HasEdges;
}
