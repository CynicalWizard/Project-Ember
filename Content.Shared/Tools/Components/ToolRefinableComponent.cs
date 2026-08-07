using Content.Shared.Tools;
using Content.Shared.Storage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Tools.Systems;
using Content.Shared.Ember.Materials;

namespace Content.Shared.Tools.Components;

/// <summary>
/// Used for something that can be refined by welder.
/// For example, glass shard can be refined to glass sheet.
/// </summary>
// Ember: procedural debris decides what it refines into from its material, so it writes RefineResult too.
[RegisterComponent, NetworkedComponent, Access(typeof(ToolRefinableSystem), typeof(EmberProceduralShardSystem))]
public sealed partial class ToolRefinableComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntitySpawnEntry> RefineResult;

    [DataField]
    public float RefineTime = 2f;

    [DataField]
    public float RefineFuel = 3f;

    [DataField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Welding";
}
