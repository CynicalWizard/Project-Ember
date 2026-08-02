using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Ember.Structures;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Ember.Walls;

/// <summary>
/// Refuses to build where a wall or a low wall already stands. A tile holds one or the other, never both.
/// </summary>
/// <remarks>
/// TileNotBlocked cannot express this. It tests the Impassable collision group, and a low wall deliberately sits
/// on the table layer so that it can be climbed over and shot across, which leaves it invisible to that check —
/// a wall could be raised straight through one, and the two would stand on the same tile.
/// </remarks>
[UsedImplicitly]
[DataDefinition]
public sealed partial class EmberNoWallInTile : IConstructionCondition
{
    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entities = IoCManager.Resolve<IEntityManager>();

        if (!entities.TryGetComponent(location.EntityId, out MapGridComponent? grid))
            return true;

        var mapSystem = entities.System<SharedMapSystem>();
        var wallQuery = entities.GetEntityQuery<EmberProceduralWallComponent>();
        var structureQuery = entities.GetEntityQuery<EmberProceduralStructureComponent>();

        var anchored = mapSystem.GetAnchoredEntitiesEnumerator(
            location.EntityId,
            grid,
            mapSystem.TileIndicesFor(location.EntityId, grid, location));

        while (anchored.MoveNext(out var entity))
        {
            if (wallQuery.HasComponent(entity.Value))
                return false;

            if (structureQuery.TryGetComponent(entity.Value, out var structure) &&
                structure.Role == EmberProceduralStructureRole.WallFrame)
            {
                return false;
            }
        }

        return true;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "ember-construction-step-condition-no-wall-in-tile",
        };
    }
}
