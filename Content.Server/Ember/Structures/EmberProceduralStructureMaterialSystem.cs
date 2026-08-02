using Content.Server.Ember.Materials;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Structures;

/// <summary>
/// Gives low walls, and everything else built straight out of a material, the properties of that material.
/// </summary>
/// <remarks>
/// Radiation was wired to walls and to material doors but not to these, so a uranium low wall stood next to a
/// uranium bulkhead reading zero on a geiger counter. The material does not care what shape it was built into.
/// </remarks>
public sealed class EmberProceduralStructureMaterialSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralStructureComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<EmberProceduralStructureComponent> ent, ref MapInitEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.Material, out EmberWallMaterialPrototype? wallMaterial) ||
            wallMaterial.PhysicalMaterial is not { } physicalId ||
            !_prototype.TryIndex(physicalId, out EmberMaterialPrototype? material))
        {
            return;
        }

        EmberMaterialRadiation.Apply(EntityManager, ent, material.Radioactivity ?? 0f);
    }
}
