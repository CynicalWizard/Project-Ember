using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Destructible.Thresholds.Behaviors;

/// <summary>
/// Scatters debris made of whatever the thing was built from, following Bay's
/// <c>place_dismantled_product</c>: something torn apart leaves shards, and a material with nothing to shatter
/// into leaves sheets instead.
/// </summary>
/// <remarks>
/// The material is not written here because the entity already knows it. That is the point of the whole
/// procedural line: a marble wall drops marble without anyone having to remember to say so.
/// </remarks>
[Serializable]
[DataDefinition]
public sealed partial class EmberSpawnMaterialDebrisBehavior : IThresholdBehavior
{
    /// <summary>How many pieces to scatter. Bay's devastated walls drop one or two.</summary>
    [DataField]
    public MinMax Count = new(1, 2);

    /// <summary>How far from the tile's centre the pieces land.</summary>
    [DataField]
    public float Offset = 0.35f;

    /// <summary>The debris prototype, which takes its look and name from the material at spawn.</summary>
    [DataField]
    public EntProtoId Shard = "EmberShard";

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        if (!TryGetMaterial(owner, system, out var materialId, out var material))
            return;

        var transform = system.EntityManager.System<TransformSystem>();
        var position = transform.GetMapCoordinates(owner);

        var count = Count.Min >= Count.Max
            ? Count.Min
            : system.Random.Next(Count.Min, Count.Max + 1);

        for (var i = 0; i < count; i++)
        {
            var scatter = new Vector2(
                system.Random.NextFloat(-Offset, Offset),
                system.Random.NextFloat(-Offset, Offset));

            // A material that shatters into nothing still leaves something behind, just its sheet form.
            if (material.ShardType == EmberShardType.None)
            {
                if (material.StackEntity is { } sheet)
                    system.EntityManager.SpawnEntity(sheet, position.Offset(scatter));

                continue;
            }

            SpawnShard(system.EntityManager, materialId, position.Offset(scatter));
        }
    }

    /// <summary>
    /// Creates a shard already knowing what it is made of.
    /// </summary>
    /// <remarks>
    /// Spawning it and then assigning the material is too late: map init runs during the spawn, so the shard
    /// would have already named and coloured itself after the prototype's placeholder material. Every wall would
    /// have dropped steel.
    /// </remarks>
    public static EntityUid SpawnShard(
        IEntityManager entities,
        ProtoId<EmberMaterialPrototype> material,
        MapCoordinates coordinates,
        EntProtoId? shard = null)
    {
        var uid = entities.CreateEntityUninitialized(shard ?? "EmberShard", coordinates);
        entities.EnsureComponent<EmberProceduralShardComponent>(uid).Material = material;
        entities.InitializeAndStartEntity(uid);

        return uid;
    }

    /// <summary>
    /// Walls name a wall material that points at the physical one; anything else carrying a material names it
    /// directly.
    /// </summary>
    private static bool TryGetMaterial(
        EntityUid owner,
        DestructibleSystem system,
        out ProtoId<EmberMaterialPrototype> id,
        [NotNullWhen(true)] out EmberMaterialPrototype? material)
    {
        id = default;
        material = null;

        var entities = system.EntityManager;
        ProtoId<EmberMaterialPrototype>? found = null;

        if (entities.TryGetComponent(owner, out EmberProceduralWallComponent? wall) &&
            system.PrototypeManager.TryIndex(wall.Material, out EmberWallMaterialPrototype? wallMaterial))
        {
            found = wallMaterial.PhysicalMaterial;
        }
        else if (entities.TryGetComponent(owner, out EmberMaterialStackComponent? stack))
        {
            found = stack.Material;
        }

        if (found is not { } materialId || !system.PrototypeManager.TryIndex(materialId, out material))
            return false;

        id = materialId;
        return true;
    }
}
