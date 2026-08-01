using System.Linq;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client.Ember.Doors;

/// <summary>
/// Turns airlocks and firelocks to face the walls they are set into, the way Bay's doors pick their dir in
/// <c>on_update_icon</c> instead of trusting whatever rotation a mapper left behind.
/// </summary>
public sealed class EmberProceduralDoorFacingSystem : EntitySystem
{
    private readonly Queue<EntityUid> _dirty = new();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<EmberProceduralWallComponent> _wallQuery;
    private EntityQuery<EmberProceduralStructureComponent> _structureQuery;
    private EntityQuery<EmberProceduralAirlockComponent> _airlockQuery;
    private EntityQuery<EmberProceduralFirelockComponent> _firelockQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _wallQuery = GetEntityQuery<EmberProceduralWallComponent>();
        _structureQuery = GetEntityQuery<EmberProceduralStructureComponent>();
        _airlockQuery = GetEntityQuery<EmberProceduralAirlockComponent>();
        _firelockQuery = GetEntityQuery<EmberProceduralFirelockComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        while (_dirty.TryDequeue(out var uid))
        {
            UpdateFacing(uid);
        }
    }

    /// <summary>
    /// Recomputes which RSI direction the door draws. Safe to call on anything; doors that have not opted in are
    /// left alone.
    /// </summary>
    public void UpdateFacing(EntityUid uid)
    {
        if (ResolveTargets(uid) is not { } targets ||
            !_spriteQuery.TryGetComponent(uid, out var sprite))
        {
            return;
        }

        if (!_xformQuery.TryGetComponent(uid, out var xform) ||
            !xform.Anchored ||
            !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            ApplyOffset(sprite, EmberDoorDirOffset.None);
            return;
        }

        var pos = grid.TileIndicesFor(xform.Coordinates);
        var vertical = Blends(uid, grid, pos.Offset(Direction.North), targets)
                       || Blends(uid, grid, pos.Offset(Direction.South), targets);
        var horizontal = Blends(uid, grid, pos.Offset(Direction.East), targets)
                         || Blends(uid, grid, pos.Offset(Direction.West), targets);

        // The neighbour scan is in grid space, so the offset is measured against the entity's grid-local
        // rotation. The renderer then adds the grid and eye rotation on top, which is what keeps the door
        // square to its wall while the player spins the camera.
        var facing = EmberProceduralDoorFacing.FacingFor(vertical, horizontal);
        var offset = EmberProceduralDoorFacing.OffsetFor(xform.LocalRotation.GetCardinalDir(), facing);

        ApplyOffset(sprite, offset);
    }

    private static void ApplyOffset(SpriteComponent sprite, EmberDoorDirOffset offset)
    {
        var value = offset switch
        {
            EmberDoorDirOffset.Clockwise => SpriteComponent.DirectionOffset.Clockwise,
            EmberDoorDirOffset.CounterClockwise => SpriteComponent.DirectionOffset.CounterClockwise,
            EmberDoorDirOffset.Flip => SpriteComponent.DirectionOffset.Flip,
            _ => SpriteComponent.DirectionOffset.None,
        };

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetDirOffset(i, value);
        }
    }

    /// <summary>
    /// Queues a re-check of every self-orienting door cardinally adjacent to <paramref name="pos"/>. Called by
    /// <see cref="Content.Client.Ember.Walls.EmberProceduralWallSystem"/> when a neighbouring tile changes.
    /// </summary>
    public void DirtyDoorsAround(MapGridComponent grid, Vector2i pos)
    {
        DirtyDoors(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.North)));
        DirtyDoors(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.South)));
        DirtyDoors(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.East)));
        DirtyDoors(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.West)));
    }

    public void DirtyDoor(EntityUid uid)
    {
        _dirty.Enqueue(uid);
    }

    private EmberDoorBlendTargets? ResolveTargets(EntityUid uid)
    {
        if (_firelockQuery.TryGetComponent(uid, out var firelock))
            return firelock.Enabled ? EmberDoorBlendTargets.Firelock : null;

        if (_airlockQuery.TryGetComponent(uid, out var airlock))
            return airlock.Enabled ? EmberDoorBlendTargets.Airlock : null;

        return null;
    }

    private bool Blends(EntityUid uid, MapGridComponent grid, Vector2i pos, EmberDoorBlendTargets targets)
    {
        var candidates = grid.GetAnchoredEntitiesEnumerator(pos);

        while (candidates.MoveNext(out var entity))
        {
            if (entity == uid)
                continue;

            // Walls are not on any blend_objects list; Bay checks the turf for them separately.
            if (_wallQuery.HasComponent(entity))
                return true;

            if (_structureQuery.TryGetComponent(entity, out var structure))
            {
                var flag = structure.Role switch
                {
                    EmberProceduralStructureRole.WallFrame => EmberDoorBlendTargets.WallFrames,
                    EmberProceduralStructureRole.Window => EmberDoorBlendTargets.Windows,
                    EmberProceduralStructureRole.Grille => EmberDoorBlendTargets.Grilles,
                    _ => EmberDoorBlendTargets.None,
                };

                if ((targets & flag) != 0)
                    return true;

                continue;
            }

            if ((targets & EmberDoorBlendTargets.Firelocks) != 0 && _firelockQuery.HasComponent(entity))
                return true;
        }

        return false;
    }

    private void DirtyDoors(AnchoredEntitiesEnumerator entities)
    {
        while (entities.MoveNext(out var entity))
        {
            if (_firelockQuery.HasComponent(entity.Value) || _airlockQuery.HasComponent(entity.Value))
                _dirty.Enqueue(entity.Value);
        }
    }
}
