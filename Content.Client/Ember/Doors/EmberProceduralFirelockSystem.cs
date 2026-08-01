using Content.Client.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client.Ember.Doors;

/// <summary>
/// Ports SierraBay's firedoor visuals: the shutter picks its facing from the walls it is embedded in, and the
/// unlit layer is the pressure alert lamp rather than the vanilla opening/closing glow.
/// </summary>
public sealed class EmberProceduralFirelockSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    private readonly Queue<EntityUid> _dirty = new();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<EmberProceduralFirelockComponent> _firelockQuery;
    private EntityQuery<EmberProceduralWallComponent> _wallQuery;
    private EntityQuery<EmberProceduralStructureComponent> _structureQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _firelockQuery = GetEntityQuery<EmberProceduralFirelockComponent>();
        _wallQuery = GetEntityQuery<EmberProceduralWallComponent>();
        _structureQuery = GetEntityQuery<EmberProceduralStructureComponent>();

        SubscribeLocalEvent<EmberProceduralFirelockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralFirelockComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralFirelockComponent, AppearanceChangeEvent>(
            OnAppearanceChange,
            after: [typeof(DoorSystem), typeof(FirelockSystem)]);

        // Anything the shutter blends with can change its facing. Walls, low walls, windows and doors all funnel
        // through EmberProceduralWallSystem's neighbour sweep, so that is where we get told about it.
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        while (_dirty.TryDequeue(out var uid))
        {
            if (!_firelockQuery.TryGetComponent(uid, out var firelock) ||
                !firelock.Running ||
                !firelock.Enabled ||
                !_spriteQuery.TryGetComponent(uid, out var sprite))
            {
                continue;
            }

            UpdateDirection(uid, sprite);
        }
    }

    private void OnStartup(EntityUid uid, EmberProceduralFirelockComponent component, ComponentStartup args)
    {
        if (!component.Enabled)
            return;

        // Vanilla only builds a denying animation for airlocks, so the hazard shutter supplies its own.
        if (TryComp<DoorComponent>(uid, out var door))
        {
            door.DenyingAnimation = new Animation
            {
                Length = door.DenyDuration,
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = DoorVisualLayers.Base,
                        KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(component.DenyState, 0f) },
                    },
                },
            };
        }

        if (!_spriteQuery.TryGetComponent(uid, out var sprite))
            return;

        // The hazard sheet has no unlit open/close glow, only the Bay alert lamp.
        if (sprite.LayerMapTryGet(DoorVisualLayers.BaseUnlit, out _))
            sprite.LayerSetState(DoorVisualLayers.BaseUnlit, component.AlertState);

        UpdateDirection(uid, sprite);
        UpdateAlert(uid, component, sprite);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralFirelockComponent component, ref AnchorStateChangedEvent args)
    {
        if (component.Enabled)
            _dirty.Enqueue(uid);
    }

    private void OnAppearanceChange(EntityUid uid, EmberProceduralFirelockComponent component, ref AppearanceChangeEvent args)
    {
        if (!component.Enabled || args.Sprite == null)
            return;

        UpdateAlert(uid, component, args.Sprite, args.Component);
    }

    /// <summary>
    /// Bay drives the alert lamp off the pressure differential; SS14 only tracks a single "firelock is holding"
    /// flag, so that is what lights it up. Vanilla also flashes the unlit layer while the door moves, which the
    /// hazard sheet has no frames for, hence overriding <see cref="FirelockSystem"/> here.
    /// </summary>
    private void UpdateAlert(
        EntityUid uid,
        EmberProceduralFirelockComponent component,
        SpriteComponent sprite,
        AppearanceComponent? appearance = null)
    {
        if (!sprite.LayerMapTryGet(DoorVisualLayers.BaseUnlit, out _))
            return;

        var alarmed = _appearance.TryGetData<bool>(uid, DoorVisuals.ClosedLights, out var closedLights, appearance)
                      && closedLights;

        sprite.LayerSetState(DoorVisualLayers.BaseUnlit, component.AlertState);
        sprite.LayerSetVisible(DoorVisualLayers.BaseUnlit, alarmed);
    }

    /// <summary>
    /// Mirrors Bay's <c>firedoor/on_update_icon</c>: a shutter that only touches walls to the north and/or south
    /// faces east, everything else faces south.
    /// </summary>
    private void UpdateDirection(EntityUid uid, SpriteComponent sprite)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform) ||
            !xform.Anchored ||
            !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            sprite.EnableDirectionOverride = false;
            return;
        }

        var pos = grid.TileIndicesFor(xform.Coordinates);
        var vertical = Blends(grid, pos.Offset(Direction.North)) || Blends(grid, pos.Offset(Direction.South));
        var horizontal = Blends(grid, pos.Offset(Direction.East)) || Blends(grid, pos.Offset(Direction.West));

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = EmberProceduralFirelockVisuals.FacingFor(vertical, horizontal);
    }

    /// <summary>
    /// Bay blends firedoors with walls, low wall frames, windows and other firedoors — notably not with grilles
    /// or airlocks.
    /// </summary>
    private bool Blends(MapGridComponent grid, Vector2i pos)
    {
        var candidates = grid.GetAnchoredEntitiesEnumerator(pos);

        while (candidates.MoveNext(out var entity))
        {
            if (_wallQuery.HasComponent(entity) || _firelockQuery.HasComponent(entity))
                return true;

            if (_structureQuery.TryGetComponent(entity, out var structure) &&
                structure.Role is EmberProceduralStructureRole.WallFrame or EmberProceduralStructureRole.Window)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Queues a re-check of every shutter cardinally adjacent to <paramref name="pos"/>. Called by
    /// <see cref="Content.Client.Ember.Walls.EmberProceduralWallSystem"/> whenever something on that tile changed
    /// what the neighbours look like.
    /// </summary>
    public void DirtyFirelocksAround(MapGridComponent grid, Vector2i pos)
    {
        DirtyFirelocks(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.North)));
        DirtyFirelocks(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.South)));
        DirtyFirelocks(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.East)));
        DirtyFirelocks(grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.West)));
    }

    private void DirtyFirelocks(AnchoredEntitiesEnumerator entities)
    {
        while (entities.MoveNext(out var entity))
        {
            if (_firelockQuery.HasComponent(entity.Value))
                _dirty.Enqueue(entity.Value);
        }
    }
}
