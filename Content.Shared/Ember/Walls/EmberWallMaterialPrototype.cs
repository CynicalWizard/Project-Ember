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

    /// <summary>
    /// Overrides the physical material's <see cref="EmberMaterialPrototype.Color"/>. Only the glass wall
    /// materials need it, since they have no physical material to take a colour from.
    /// </summary>
    /// <remarks>
    /// This used to be a plain colour set on both prototypes, and the two drifted: diamond walls came out
    /// turquoise against pale lilac diamond doors, and wood walls were several shades darker than wood
    /// everything else. One material now has one colour unless something deliberately says otherwise.
    /// </remarks>
    [DataField]
    public Color? Color { get; set; }

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
