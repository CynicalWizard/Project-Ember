using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
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
    private static readonly ProtoId<EmberMaterialPrototype> TableFrame = "Steel";

    /// <summary>How many pieces to scatter. Bay's devastated walls drop one or two.</summary>
    [DataField]
    public MinMax Count = new(1, 2);

    /// <summary>How far from the tile's centre the pieces land.</summary>
    [DataField]
    public float Offset = 0.35f;

    /// <summary>The debris prototype, which takes its look and name from the material at spawn.</summary>
    [DataField]
    public EntProtoId Shard = "EmberShard";

    /// <summary>
    /// The chance a piece survives whole and comes back as a sheet instead. Bay pays this out per part of a
    /// broken table; a wall that comes down leaves nothing intact, which is the default.
    /// </summary>
    [DataField]
    public float SheetChance;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var transform = system.EntityManager.System<TransformSystem>();
        var position = transform.GetMapCoordinates(owner);

        var count = Count.Min >= Count.Max
            ? Count.Min
            : system.Random.Next(Count.Min, Count.Max + 1);

        foreach (var materialId in GetMaterials(owner, system))
        {
            if (!system.PrototypeManager.TryIndex(materialId, out EmberMaterialPrototype? material))
                continue;

            for (var i = 0; i < count; i++)
            {
                var scatter = new Vector2(
                    system.Random.NextFloat(-Offset, Offset),
                    system.Random.NextFloat(-Offset, Offset));

                // A material that shatters into nothing still leaves something behind, just its sheet form,
                // as does the occasional piece that comes through the wreck intact.
                var whole = material.ShardType == EmberShardType.None ||
                            (SheetChance > 0f && system.Random.NextFloat() < SheetChance);

                if (whole)
                {
                    if (material.StackEntity is { } sheet)
                        system.EntityManager.SpawnEntity(sheet, position.Offset(scatter));

                    continue;
                }

                SpawnShard(system.EntityManager, materialId, position.Offset(scatter));
            }
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
    /// Everything the thing was built out of. Most objects are one material; a table is its frame, its plating
    /// and whatever reinforces the plating, and Bay's <c>break_to_parts</c> settles up with each of them.
    /// </summary>
    /// <remarks>
    /// Walls name a wall material that points at the physical one; anything else carrying a material names it
    /// directly.
    /// </remarks>
    private static IEnumerable<ProtoId<EmberMaterialPrototype>> GetMaterials(
        EntityUid owner,
        DestructibleSystem system)
    {
        var entities = system.EntityManager;

        if (entities.TryGetComponent(owner, out EmberProceduralWallComponent? wall) &&
            system.PrototypeManager.TryIndex(wall.Material, out EmberWallMaterialPrototype? wallMaterial))
        {
            if (wallMaterial.PhysicalMaterial is { } physical)
                yield return physical;

            yield break;
        }

        if (entities.TryGetComponent(owner, out EmberProceduralTableComponent? table))
        {
            // The frame under the plating is steel whatever the table is topped with, exactly as on Bay.
            yield return TableFrame;

            if (table.Material is { } plating)
                yield return plating;

            if (table.Reinforcement is { } reinforcement)
                yield return reinforcement;

            yield break;
        }

        if (entities.TryGetComponent(owner, out EmberMaterialStackComponent? stack) &&
            stack.Material is { } material)
        {
            yield return material;
        }
    }
}
