using System.Numerics;
using Content.Client.Ember.Walls;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.Ember.Structures;

public sealed class EmberProceduralStructureSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EmberProceduralWallSystem _wallSystem = default!;

    private readonly Queue<EntityUid> _dirty = new();
    private readonly Queue<EntityUid> _anchorChanged = new();
    private int _generation;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralStructureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralStructureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmberProceduralStructureComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralStructureComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<DoorComponent, ComponentStartup>(OnDoorStartup);
        SubscribeLocalEvent<DoorComponent, ComponentShutdown>(OnDoorShutdown);
        SubscribeLocalEvent<DoorComponent, AnchorStateChangedEvent>(OnDoorAnchorChanged);
    }

    private void OnStartup(EntityUid uid, EmberProceduralStructureComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        if (xform.Anchored && TryComp<MapGridComponent>(xform.GridUid, out var grid))
            component.LastPosition = (xform.GridUid.Value, grid.TileIndicesFor(xform.Coordinates));

        SetupLayers(uid, component);
        DirtyNeighbours(uid, component);
        _wallSystem.DirtyWallsAround(uid);
    }

    private void OnShutdown(EntityUid uid, EmberProceduralStructureComponent component, ComponentShutdown args)
    {
        _dirty.Enqueue(uid);
        DirtyNeighbours(uid, component);
        _wallSystem.DirtyWallsAround(uid, component.LastPosition);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralStructureComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Detaching)
        {
            DirtyNeighbours(uid, component);
            _wallSystem.DirtyWallsAround(uid, component.LastPosition);
            return;
        }

        _anchorChanged.Enqueue(uid);
        _wallSystem.DirtyWallsAround(uid);
    }

    private void OnAfterAutoHandleState(EntityUid uid, EmberProceduralStructureComponent component, ref AfterAutoHandleStateEvent args)
    {
        _dirty.Enqueue(uid);
    }

    private void OnDoorStartup(EntityUid uid, DoorComponent component, ComponentStartup args)
    {
        DirtyNeighboursAround(uid);
        _wallSystem.DirtyWallsAround(uid);
    }

    private void OnDoorShutdown(EntityUid uid, DoorComponent component, ComponentShutdown args)
    {
        DirtyNeighboursAround(uid);
        _wallSystem.DirtyWallsAround(uid);
    }

    private void OnDoorAnchorChanged(EntityUid uid, DoorComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Detaching)
        {
            DirtyNeighboursAround(uid);
            _wallSystem.DirtyWallsAround(uid);
            return;
        }

        DirtyNeighboursAround(uid);
        _wallSystem.DirtyWallsAround(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var xformQuery = GetEntityQuery<TransformComponent>();
        var structureQuery = GetEntityQuery<EmberProceduralStructureComponent>();

        while (_anchorChanged.TryDequeue(out var uid))
        {
            if (xformQuery.TryGetComponent(uid, out var xform))
                DirtyNeighbours(uid, transform: xform, structureQuery: structureQuery);
        }

        if (_dirty.Count == 0)
            return;

        _generation++;
        var spriteQuery = GetEntityQuery<SpriteComponent>();
        var wallQuery = GetEntityQuery<EmberProceduralWallComponent>();
        var doorQuery = GetEntityQuery<DoorComponent>();

        while (_dirty.TryDequeue(out var uid))
        {
            UpdateSprite(uid, spriteQuery, structureQuery, wallQuery, doorQuery, xformQuery);
        }
    }

    private void SetupLayers(EntityUid uid, EmberProceduralStructureComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        foreach (var layer in AllLayers)
        {
            sprite.LayerMapRemove(layer.Layer);
            var index = sprite.AddLayer(new SpriteSpecifier.Rsi(component.Sprite, StateFor(component, false, false, 0)));
            sprite.LayerMapSet(layer.Layer, index);
            sprite.LayerSetDirOffset(layer.Layer, layer.Offset);
        }
    }

    private void UpdateSprite(
        EntityUid uid,
        EntityQuery<SpriteComponent> spriteQuery,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<DoorComponent> doorQuery,
        EntityQuery<TransformComponent> xformQuery)
    {
        if (!structureQuery.TryGetComponent(uid, out var component) ||
            component.UpdateGeneration == _generation ||
            !spriteQuery.TryGetComponent(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberWallMaterialPrototype? material) ||
            !xformQuery.TryGetComponent(uid, out var xform))
            return;

        component.UpdateGeneration = _generation;
        var color = (component.Color ?? material.Color).WithAlpha(component.Alpha);

        MapGridComponent? grid = null;
        if (xform.Anchored && !TryComp(xform.GridUid, out grid))
            return;

        var onFrame = grid != null && component.Role != EmberProceduralStructureRole.WallFrame && IsOnFrame(uid, grid, xform, structureQuery);

        if (component.Broken)
        {
            SetSingleState(sprite, onFrame ? component.BrokenOnFrameState : component.BrokenState, color);
            return;
        }

        var corners = grid == null
            ? default
            : CalculateCornerFill(uid, grid, component, xform, structureQuery, wallQuery, doorQuery);

        SetCorner(sprite, component, EmberStructureLayer.SE, corners.SE, onFrame, color);
        SetCorner(sprite, component, EmberStructureLayer.NE, corners.NE, onFrame, color);
        SetCorner(sprite, component, EmberStructureLayer.NW, corners.NW, onFrame, color);
        SetCorner(sprite, component, EmberStructureLayer.SW, corners.SW, onFrame, color);
    }

    private void DirtyNeighbours(
        EntityUid uid,
        EmberProceduralStructureComponent? component = null,
        TransformComponent? transform = null,
        EntityQuery<EmberProceduralStructureComponent>? structureQuery = null)
    {
        structureQuery ??= GetEntityQuery<EmberProceduralStructureComponent>();
        if (!structureQuery.Value.Resolve(uid, ref component, false))
            return;

        _dirty.Enqueue(uid);

        if (!Resolve(uid, ref transform, false))
            return;

        Vector2i pos;
        MapGridComponent? grid;

        if (transform.Anchored && TryComp(transform.GridUid, out grid))
        {
            pos = grid.TileIndicesFor(transform.Coordinates);
            component.LastPosition = (transform.GridUid.Value, pos);
        }
        else
        {
            if (component.LastPosition is not (EntityUid gridId, Vector2i oldPos) ||
                !TryComp(gridId, out grid))
                return;

            pos = oldPos;
        }

        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, -1)));
    }

    private void DirtyEntities(AnchoredEntitiesEnumerator entities)
    {
        while (entities.MoveNext(out var entity))
            _dirty.Enqueue(entity.Value);
    }

    private EmberProceduralStructureLayerCorners<ConnectionCorner> CalculateCornerFill(
        EntityUid uid,
        MapGridComponent grid,
        EmberProceduralStructureComponent component,
        TransformComponent xform,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        var pos = grid.TileIndicesFor(xform.Coordinates);
        var n = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.North)), structureQuery, wallQuery, doorQuery);
        var ne = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.NorthEast)), structureQuery, wallQuery, doorQuery);
        var e = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.East)), structureQuery, wallQuery, doorQuery);
        var se = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.SouthEast)), structureQuery, wallQuery, doorQuery);
        var s = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.South)), structureQuery, wallQuery, doorQuery);
        var sw = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.SouthWest)), structureQuery, wallQuery, doorQuery);
        var w = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.West)), structureQuery, wallQuery, doorQuery);
        var nw = MatchingConnection(uid, component, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.NorthWest)), structureQuery, wallQuery, doorQuery);

        var cornerNE = ConnectionCorner.None;
        var cornerSE = ConnectionCorner.None;
        var cornerSW = ConnectionCorner.None;
        var cornerNW = ConnectionCorner.None;

        ApplyCardinal(n, ref cornerNE.CounterClockwise, ref cornerNW.Clockwise);
        ApplyDiagonal(ne, ref cornerNE.Diagonal);
        ApplyCardinal(e, ref cornerNE.Clockwise, ref cornerSE.CounterClockwise);
        ApplyDiagonal(se, ref cornerSE.Diagonal);
        ApplyCardinal(s, ref cornerSE.Clockwise, ref cornerSW.CounterClockwise);
        ApplyDiagonal(sw, ref cornerSW.Diagonal);
        ApplyCardinal(w, ref cornerSW.Clockwise, ref cornerNW.CounterClockwise);
        ApplyDiagonal(nw, ref cornerNW.Diagonal);

        return EmberProceduralStructureCorners.MapToLayers(
            xform.LocalRotation.GetCardinalDir(),
            cornerSE,
            cornerNE,
            cornerNW,
            cornerSW);
    }

    private ConnectionKind MatchingConnection(
        EntityUid uid,
        EmberProceduralStructureComponent component,
        AnchoredEntitiesEnumerator candidates,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        var foundOther = false;

        while (candidates.MoveNext(out var candidate))
        {
            if (candidate == uid)
                continue;

            if (structureQuery.TryGetComponent(candidate, out var other))
            {
                if (other.Role == EmberProceduralStructureRole.WallFrame)
                    return ConnectionKind.Full;

                if (other.Role == component.Role)
                    return ConnectionKind.Full;

                foundOther = true;
                continue;
            }

            if (wallQuery.HasComponent(candidate) || doorQuery.HasComponent(candidate))
                foundOther = true;
        }

        return foundOther ? ConnectionKind.Other : ConnectionKind.None;
    }

    private bool IsOnFrame(
        EntityUid uid,
        MapGridComponent grid,
        TransformComponent xform,
        EntityQuery<EmberProceduralStructureComponent> structureQuery)
    {
        var pos = grid.TileIndicesFor(xform.Coordinates);
        var candidates = grid.GetAnchoredEntitiesEnumerator(pos);

        while (candidates.MoveNext(out var candidate))
        {
            if (candidate == uid)
                continue;

            if (structureQuery.TryGetComponent(candidate, out var other) &&
                other.Role == EmberProceduralStructureRole.WallFrame)
                return true;
        }

        return false;
    }

    private void DirtyNeighboursAround(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? transform) ||
            !transform.Anchored ||
            !TryComp<MapGridComponent>(transform.GridUid, out var grid))
            return;

        var pos = grid.TileIndicesFor(transform.Coordinates);

        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, -1)));
    }

    private static void ApplyCardinal(ConnectionKind connection, ref ConnectionKind first, ref ConnectionKind second)
    {
        if (connection == ConnectionKind.None)
            return;

        first = connection;
        second = connection;
    }

    private static void ApplyDiagonal(ConnectionKind connection, ref ConnectionKind diagonal)
    {
        if (connection != ConnectionKind.None)
            diagonal = connection;
    }

    private static void SetSingleState(SpriteComponent sprite, string state, Color color)
    {
        foreach (var layer in AllLayers)
        {
            sprite.LayerSetColor(layer.Layer, color);

            var visible = layer.Layer == EmberStructureLayer.SE;
            sprite.LayerSetVisible(layer.Layer, visible);

            if (visible)
                sprite.LayerSetState(layer.Layer, state);
        }
    }

    private static void SetCorner(
        SpriteComponent sprite,
        EmberProceduralStructureComponent component,
        EmberStructureLayer layer,
        ConnectionCorner corner,
        bool onFrame,
        Color color)
    {
        sprite.LayerSetState(layer, StateFor(component, corner.HasOther, onFrame, corner.State));
        sprite.LayerSetColor(layer, color);
        sprite.LayerSetVisible(layer, true);
    }

    private static string StateFor(EmberProceduralStructureComponent component, bool other, bool onFrame, int corner)
    {
        if (component.Role == EmberProceduralStructureRole.WallFrame)
            return other ? $"frame_other{corner}" : $"frame{corner}";

        var suffix = (other, onFrame) switch
        {
            (true, true) => "_other_onframe",
            (false, true) => "_onframe",
            (true, false) => "_other",
            _ => string.Empty,
        };

        return $"{component.StateBase}{suffix}{corner}";
    }

    private static readonly StructureLayerDefinition[] AllLayers =
    [
        new(EmberStructureLayer.SE, DirectionOffset.None),
        new(EmberStructureLayer.NE, DirectionOffset.CounterClockwise),
        new(EmberStructureLayer.NW, DirectionOffset.Flip),
        new(EmberStructureLayer.SW, DirectionOffset.Clockwise),
    ];

    private readonly record struct StructureLayerDefinition(EmberStructureLayer Layer, DirectionOffset Offset);

    private struct ConnectionCorner
    {
        public ConnectionKind CounterClockwise;
        public ConnectionKind Diagonal;
        public ConnectionKind Clockwise;

        public int State =>
            (CounterClockwise != ConnectionKind.None ? 1 : 0) |
            (Diagonal != ConnectionKind.None ? 2 : 0) |
            (Clockwise != ConnectionKind.None ? 4 : 0);

        public bool HasOther =>
            CounterClockwise == ConnectionKind.Other ||
            Diagonal == ConnectionKind.Other ||
            Clockwise == ConnectionKind.Other;

        public static ConnectionCorner None => default;
    }

    private enum EmberStructureLayer : byte
    {
        SE,
        NE,
        NW,
        SW,
    }

    private enum ConnectionKind : byte
    {
        None,
        Full,
        Other,
    }
}
