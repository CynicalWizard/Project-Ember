using Content.Shared.Doors.Components;
using Content.Shared.Ember.Structures;
using System.Numerics;
using Content.Shared.Ember.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.Ember.Walls;

public sealed class EmberProceduralWallSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly Queue<EntityUid> _dirty = new();
    private readonly Queue<EntityUid> _anchorChanged = new();
    private int _generation;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralWallComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralWallComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmberProceduralWallComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralWallComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(EntityUid uid, EmberProceduralWallComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        if (xform.Anchored && TryComp<MapGridComponent>(xform.GridUid, out var grid))
            component.LastPosition = (xform.GridUid.Value, grid.TileIndicesFor(xform.Coordinates));

        SetupLayers(uid, component);
        DirtyNeighbours(uid, component);
    }

    private void OnShutdown(EntityUid uid, EmberProceduralWallComponent component, ComponentShutdown args)
    {
        _dirty.Enqueue(uid);
        DirtyNeighbours(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralWallComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Detaching)
        {
            DirtyNeighbours(uid, component);
            return;
        }

        _anchorChanged.Enqueue(uid);
    }

    private void OnAfterAutoHandleState(EntityUid uid, EmberProceduralWallComponent component, ref AfterAutoHandleStateEvent args)
    {
        _dirty.Enqueue(uid);
        DirtyWallsAround(uid, component.LastPosition);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var xformQuery = GetEntityQuery<TransformComponent>();
        var wallQuery = GetEntityQuery<EmberProceduralWallComponent>();

        while (_anchorChanged.TryDequeue(out var uid))
        {
            if (xformQuery.TryGetComponent(uid, out var xform))
                DirtyNeighbours(uid, transform: xform, wallQuery: wallQuery);
        }

        if (_dirty.Count == 0)
            return;

        _generation++;
        var spriteQuery = GetEntityQuery<SpriteComponent>();

        while (_dirty.TryDequeue(out var uid))
        {
            UpdateSprite(uid, spriteQuery, wallQuery, xformQuery);
        }
    }

    private void SetupLayers(EntityUid uid, EmberProceduralWallComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberWallMaterialPrototype? material))
            return;

        var visuals = EmberProceduralWallVisuals.Resolve(component, material);

        foreach (var layer in AllLayers)
        {
            sprite.LayerMapRemove(layer.Layer);
            var index = sprite.AddLayer(new SpriteSpecifier.Rsi(component.Sprite, InitialStateFor(layer.Kind, visuals)));
            sprite.LayerMapSet(layer.Layer, index);
            sprite.LayerSetDirOffset(layer.Layer, layer.Offset);
        }

        ApplyColors(sprite, visuals);
    }

    private void UpdateSprite(
        EntityUid uid,
        EntityQuery<SpriteComponent> spriteQuery,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<TransformComponent> xformQuery)
    {
        if (!wallQuery.TryGetComponent(uid, out var wall) ||
            !wall.Running ||
            wall.UpdateGeneration == _generation ||
            !spriteQuery.TryGetComponent(uid, out var sprite) ||
            !_prototype.TryIndex(wall.Material, out EmberWallMaterialPrototype? material))
            return;

        wall.UpdateGeneration = _generation;
        var visuals = EmberProceduralWallVisuals.Resolve(wall, material);

        if (!xformQuery.TryGetComponent(uid, out var xform))
            return;

        MapGridComponent? grid = null;
        if (xform.Anchored && !TryComp(xform.GridUid, out grid))
            return;

        var (cornerNE, cornerNW, cornerSW, cornerSE) = grid == null
            ? (CornerFill.None, CornerFill.None, CornerFill.None, CornerFill.None)
            : CalculateCornerFill(grid, visuals, xform);

        SetCorner(sprite, EmberWallLayer.BaseNE, WallLayerKind.Base, visuals, cornerNE);
        SetCorner(sprite, EmberWallLayer.BaseSE, WallLayerKind.Base, visuals, cornerSE);
        SetCorner(sprite, EmberWallLayer.BaseSW, WallLayerKind.Base, visuals, cornerSW);
        SetCorner(sprite, EmberWallLayer.BaseNW, WallLayerKind.Base, visuals, cornerNW);

        SetCorner(sprite, EmberWallLayer.PaintNE, WallLayerKind.Paint, visuals, cornerNE);
        SetCorner(sprite, EmberWallLayer.PaintSE, WallLayerKind.Paint, visuals, cornerSE);
        SetCorner(sprite, EmberWallLayer.PaintSW, WallLayerKind.Paint, visuals, cornerSW);
        SetCorner(sprite, EmberWallLayer.PaintNW, WallLayerKind.Paint, visuals, cornerNW);

        SetCorner(sprite, EmberWallLayer.StripeNE, WallLayerKind.Stripe, visuals, cornerNE);
        SetCorner(sprite, EmberWallLayer.StripeSE, WallLayerKind.Stripe, visuals, cornerSE);
        SetCorner(sprite, EmberWallLayer.StripeSW, WallLayerKind.Stripe, visuals, cornerSW);
        SetCorner(sprite, EmberWallLayer.StripeNW, WallLayerKind.Stripe, visuals, cornerNW);

        SetCorner(sprite, EmberWallLayer.ReinforcementNE, WallLayerKind.Reinforcement, visuals, cornerNE);
        SetCorner(sprite, EmberWallLayer.ReinforcementSE, WallLayerKind.Reinforcement, visuals, cornerSE);
        SetCorner(sprite, EmberWallLayer.ReinforcementSW, WallLayerKind.Reinforcement, visuals, cornerSW);
        SetCorner(sprite, EmberWallLayer.ReinforcementNW, WallLayerKind.Reinforcement, visuals, cornerNW);

        ApplyColors(sprite, visuals);
    }

    private void DirtyNeighbours(
        EntityUid uid,
        EmberProceduralWallComponent? component = null,
        TransformComponent? transform = null,
        EntityQuery<EmberProceduralWallComponent>? wallQuery = null)
    {
        wallQuery ??= GetEntityQuery<EmberProceduralWallComponent>();
        if (!wallQuery.Value.Resolve(uid, ref component, false) || !component.Running)
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

    public void DirtyWallsAround(EntityUid uid, (EntityUid?, Vector2i)? lastPosition = null)
    {
        Vector2i pos;
        MapGridComponent? grid;

        if (TryComp(uid, out TransformComponent? transform) &&
            transform.Anchored &&
            TryComp(transform.GridUid, out grid))
        {
            pos = grid.TileIndicesFor(transform.Coordinates);
        }
        else
        {
            if (lastPosition is not (EntityUid gridUid, Vector2i oldPos) ||
                !TryComp(gridUid, out grid))
                return;

            pos = oldPos;
        }

        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 0)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(0, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, -1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(-1, 1)));
        DirtyEntities(grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(1, -1)));
    }

    private (CornerFill ne, CornerFill nw, CornerFill sw, CornerFill se) CalculateCornerFill(
        MapGridComponent grid,
        EmberProceduralWallLayerVisuals visuals,
        TransformComponent xform)
    {
        var pos = grid.TileIndicesFor(xform.Coordinates);
        var n = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.North)));
        var ne = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.NorthEast)));
        var e = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.East)));
        var se = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.SouthEast)));
        var s = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.South)));
        var sw = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.SouthWest)));
        var w = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.West)));
        var nw = MatchingEntity(visuals, grid.GetAnchoredEntitiesEnumerator(pos.Offset(Direction.NorthWest)));

        var cornerNE = CornerFill.None;
        var cornerSE = CornerFill.None;
        var cornerSW = CornerFill.None;
        var cornerNW = CornerFill.None;

        if (n)
        {
            cornerNE |= CornerFill.CounterClockwise;
            cornerNW |= CornerFill.Clockwise;
        }

        if (ne)
            cornerNE |= CornerFill.Diagonal;

        if (e)
        {
            cornerNE |= CornerFill.Clockwise;
            cornerSE |= CornerFill.CounterClockwise;
        }

        if (se)
            cornerSE |= CornerFill.Diagonal;

        if (s)
        {
            cornerSE |= CornerFill.Clockwise;
            cornerSW |= CornerFill.CounterClockwise;
        }

        if (sw)
            cornerSW |= CornerFill.Diagonal;

        if (w)
        {
            cornerSW |= CornerFill.Clockwise;
            cornerNW |= CornerFill.CounterClockwise;
        }

        if (nw)
            cornerNW |= CornerFill.Diagonal;

        return xform.LocalRotation.GetCardinalDir() switch
        {
            Direction.North => (cornerSW, cornerSE, cornerNE, cornerNW),
            Direction.West => (cornerSE, cornerNE, cornerNW, cornerSW),
            Direction.South => (cornerNE, cornerNW, cornerSW, cornerSE),
            _ => (cornerNW, cornerSW, cornerSE, cornerNE),
        };
    }

    private bool MatchingEntity(EmberProceduralWallLayerVisuals visuals, AnchoredEntitiesEnumerator candidates)
    {
        while (candidates.MoveNext(out var entity))
        {
            if (HasComp<EmberProceduralStructureComponent>(entity) || HasComp<DoorComponent>(entity))
                return true;

            if (!TryComp<EmberProceduralWallComponent>(entity, out var other) ||
                !_prototype.TryIndex(other.Material, out EmberWallMaterialPrototype? otherMaterial))
                continue;

            var otherVisuals = EmberProceduralWallVisuals.Resolve(other, otherMaterial);
            if (otherVisuals.SmoothKey == visuals.SmoothKey)
                return true;
        }

        return false;
    }

    private static void ApplyColors(SpriteComponent sprite, EmberProceduralWallLayerVisuals visuals)
    {
        SetLayerGroup(sprite, BaseLayers, visuals.BaseColor, true);
        SetLayerGroup(sprite, PaintLayers, visuals.PaintColor ?? Color.White, visuals.PaintColor != null);
        SetLayerGroup(sprite, StripeLayers, visuals.StripeColor ?? Color.White, visuals.StripeColor != null);
        SetLayerGroup(sprite, ReinforcementLayers, visuals.ReinforcementColor ?? Color.White, visuals.ReinforcementColor != null);
    }

    private static void SetLayerGroup(SpriteComponent sprite, IEnumerable<EmberWallLayer> layers, Color color, bool visible)
    {
        foreach (var layer in layers)
        {
            sprite.LayerSetColor(layer, color);
            sprite.LayerSetVisible(layer, visible);
        }
    }

    private static void SetCorner(
        SpriteComponent sprite,
        EmberWallLayer layer,
        WallLayerKind kind,
        EmberProceduralWallLayerVisuals visuals,
        CornerFill corner)
    {
        sprite.LayerSetState(layer, StateFor(kind, visuals, (int) corner));
    }

    private static string InitialStateFor(WallLayerKind kind, EmberProceduralWallLayerVisuals visuals)
    {
        return StateFor(kind, visuals, 0);
    }

    private static string StateFor(WallLayerKind kind, EmberProceduralWallLayerVisuals visuals, int corner)
    {
        return kind switch
        {
            WallLayerKind.Base => EmberProceduralWallStates.Base(visuals.StateBase, corner),
            WallLayerKind.Paint => EmberProceduralWallStates.Paint(visuals.StateBase, corner, visuals.PaintColor != null),
            WallLayerKind.Stripe => EmberProceduralWallStates.Stripe(corner, visuals.StripeColor != null),
            WallLayerKind.Reinforcement => EmberProceduralWallStates.Reinforcement(visuals.ReinforcementStateBase, corner),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static readonly EmberWallLayer[] BaseLayers =
    [
        EmberWallLayer.BaseSE,
        EmberWallLayer.BaseNE,
        EmberWallLayer.BaseNW,
        EmberWallLayer.BaseSW,
    ];

    private static readonly EmberWallLayer[] PaintLayers =
    [
        EmberWallLayer.PaintSE,
        EmberWallLayer.PaintNE,
        EmberWallLayer.PaintNW,
        EmberWallLayer.PaintSW,
    ];

    private static readonly EmberWallLayer[] StripeLayers =
    [
        EmberWallLayer.StripeSE,
        EmberWallLayer.StripeNE,
        EmberWallLayer.StripeNW,
        EmberWallLayer.StripeSW,
    ];

    private static readonly EmberWallLayer[] ReinforcementLayers =
    [
        EmberWallLayer.ReinforcementSE,
        EmberWallLayer.ReinforcementNE,
        EmberWallLayer.ReinforcementNW,
        EmberWallLayer.ReinforcementSW,
    ];

    private static readonly WallLayerDefinition[] AllLayers =
    [
        new(EmberWallLayer.BaseSE, DirectionOffset.None, WallLayerKind.Base),
        new(EmberWallLayer.BaseNE, DirectionOffset.CounterClockwise, WallLayerKind.Base),
        new(EmberWallLayer.BaseNW, DirectionOffset.Flip, WallLayerKind.Base),
        new(EmberWallLayer.BaseSW, DirectionOffset.Clockwise, WallLayerKind.Base),
        new(EmberWallLayer.PaintSE, DirectionOffset.None, WallLayerKind.Paint),
        new(EmberWallLayer.PaintNE, DirectionOffset.CounterClockwise, WallLayerKind.Paint),
        new(EmberWallLayer.PaintNW, DirectionOffset.Flip, WallLayerKind.Paint),
        new(EmberWallLayer.PaintSW, DirectionOffset.Clockwise, WallLayerKind.Paint),
        new(EmberWallLayer.StripeSE, DirectionOffset.None, WallLayerKind.Stripe),
        new(EmberWallLayer.StripeNE, DirectionOffset.CounterClockwise, WallLayerKind.Stripe),
        new(EmberWallLayer.StripeNW, DirectionOffset.Flip, WallLayerKind.Stripe),
        new(EmberWallLayer.StripeSW, DirectionOffset.Clockwise, WallLayerKind.Stripe),
        new(EmberWallLayer.ReinforcementSE, DirectionOffset.None, WallLayerKind.Reinforcement),
        new(EmberWallLayer.ReinforcementNE, DirectionOffset.CounterClockwise, WallLayerKind.Reinforcement),
        new(EmberWallLayer.ReinforcementNW, DirectionOffset.Flip, WallLayerKind.Reinforcement),
        new(EmberWallLayer.ReinforcementSW, DirectionOffset.Clockwise, WallLayerKind.Reinforcement),
    ];

    private readonly record struct WallLayerDefinition(
        EmberWallLayer Layer,
        DirectionOffset Offset,
        WallLayerKind Kind);

    private enum EmberWallLayer : byte
    {
        BaseSE,
        BaseNE,
        BaseNW,
        BaseSW,
        PaintSE,
        PaintNE,
        PaintNW,
        PaintSW,
        StripeSE,
        StripeNE,
        StripeNW,
        StripeSW,
        ReinforcementSE,
        ReinforcementNE,
        ReinforcementNW,
        ReinforcementSW,
    }

    private enum WallLayerKind : byte
    {
        Base,
        Paint,
        Stripe,
        Reinforcement,
    }

    [Flags]
    private enum CornerFill : byte
    {
        None = 0,
        CounterClockwise = 1,
        Diagonal = 2,
        Clockwise = 4,
    }
}
