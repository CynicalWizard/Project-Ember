using Content.Client.Ember.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Tag;
using System.Numerics;
using System.Linq;
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
    /// <summary>
    /// Bay's <c>wall_noblend_objects</c>. Windoors sit on a tile like a full window but must not be smoothed into.
    /// </summary>
    private static readonly ProtoId<TagPrototype> NoBlendTag = "EmberWallNoBlend";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EmberProceduralDoorFacingSystem _doorFacing = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private readonly Queue<EntityUid> _dirty = new();
    private readonly Queue<EntityUid> _anchorChanged = new();
    private int _generation;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<EmberProceduralWallComponent> _wallQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<EmberProceduralStructureComponent> _structureQuery;
    private EntityQuery<DoorComponent> _doorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _wallQuery = GetEntityQuery<EmberProceduralWallComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _structureQuery = GetEntityQuery<EmberProceduralStructureComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();

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

        while (_anchorChanged.TryDequeue(out var uid))
        {
            if (_xformQuery.TryGetComponent(uid, out var xform))
                DirtyNeighbours(uid, transform: xform, wallQuery: _wallQuery);
        }

        if (_dirty.Count == 0)
            return;

        _generation++;

        while (_dirty.TryDequeue(out var uid))
        {
            UpdateSprite(uid, _spriteQuery, _wallQuery, _xformQuery, _structureQuery, _doorQuery);
        }
    }

    private void SetupLayers(EntityUid uid, EmberProceduralWallComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberWallMaterialPrototype? material))
            return;

        var visuals = EmberProceduralWallVisuals.Resolve(component, material, ResolvePhysical(material));

        // Hide default base layers if they exist (usually layer 0 defined in YAML)
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetVisible(i, false);
        }

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
        EntityQuery<TransformComponent> xformQuery,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        if (!wallQuery.TryGetComponent(uid, out var wall) ||
            !wall.Running ||
            wall.UpdateGeneration == _generation ||
            !spriteQuery.TryGetComponent(uid, out var sprite) ||
            !_prototype.TryIndex(wall.Material, out EmberWallMaterialPrototype? material))
            return;

        wall.UpdateGeneration = _generation;
        var visuals = EmberProceduralWallVisuals.Resolve(wall, material, ResolvePhysical(material));

        if (!xformQuery.TryGetComponent(uid, out var xform))
            return;

        MapGridComponent? grid = null;
        if (xform.Anchored && !TryComp(xform.GridUid, out grid))
            return;

        var connections = grid == null
            ? default
            : CalculateCornerFill(uid, grid, visuals, xform, wallQuery, structureQuery, doorQuery);

        var (cornerNE, cornerNW, cornerSW, cornerSE) = connections.Wall;
        var (otherNE, otherNW, otherSW, otherSE) = connections.Other;

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

        // The seam layer runs off its own connection set, so it gets the "other" corners rather than the wall ones.
        SetCorner(sprite, EmberWallLayer.EdgeNE, WallLayerKind.Edge, visuals, otherNE);
        SetCorner(sprite, EmberWallLayer.EdgeSE, WallLayerKind.Edge, visuals, otherSE);
        SetCorner(sprite, EmberWallLayer.EdgeSW, WallLayerKind.Edge, visuals, otherSW);
        SetCorner(sprite, EmberWallLayer.EdgeNW, WallLayerKind.Edge, visuals, otherNW);

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

        Dirty8Way(grid, pos);
    }

    private void Dirty8Way(MapGridComponent grid, Vector2i pos)
    {
        // Walls, low walls, windows and doors all reach this sweep, and every one of them can flip which way a
        // Bay airlock or hazard shutter faces.
        _doorFacing.DirtyDoorsAround(grid, pos);

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

        Dirty8Way(grid, pos);
    }

    /// <summary>
    /// Bay's <c>update_connections</c> builds two connection sets: everything the wall joins to, and the subset
    /// of those joins that get a visible seam. The base, paint, stripe and reinforcement layers use the first;
    /// the <c>_other</c> seam layer uses the second.
    /// </summary>
    private WallConnections CalculateCornerFill(
        EntityUid uid,
        MapGridComponent grid,
        EmberProceduralWallLayerVisuals visuals,
        TransformComponent xform,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        var pos = grid.TileIndicesFor(xform.Coordinates);

        EmberWallJoin Join(Direction dir) => JoinAt(
            uid,
            visuals,
            grid.GetAnchoredEntitiesEnumerator(pos.Offset(dir)),
            wallQuery,
            structureQuery,
            doorQuery);

        var n = Join(Direction.North);
        var ne = Join(Direction.NorthEast);
        var e = Join(Direction.East);
        var se = Join(Direction.SouthEast);
        var s = Join(Direction.South);
        var sw = Join(Direction.SouthWest);
        var w = Join(Direction.West);
        var nw = Join(Direction.NorthWest);

        var wall = BuildCorners(n, ne, e, se, s, sw, w, nw, join => join != EmberWallJoin.None);
        var other = BuildCorners(n, ne, e, se, s, sw, w, nw, join => join == EmberWallJoin.Edge);

        var dir = xform.LocalRotation.GetCardinalDir();
        return new WallConnections(Rotate(wall, dir), Rotate(other, dir));
    }

    private static CornerSet BuildCorners(
        EmberWallJoin n,
        EmberWallJoin ne,
        EmberWallJoin e,
        EmberWallJoin se,
        EmberWallJoin s,
        EmberWallJoin sw,
        EmberWallJoin w,
        EmberWallJoin nw,
        Func<EmberWallJoin, bool> counts)
    {
        var cornerNE = CornerFill.None;
        var cornerSE = CornerFill.None;
        var cornerSW = CornerFill.None;
        var cornerNW = CornerFill.None;

        if (counts(n))
        {
            cornerNE |= CornerFill.CounterClockwise;
            cornerNW |= CornerFill.Clockwise;
        }

        if (counts(ne))
            cornerNE |= CornerFill.Diagonal;

        if (counts(e))
        {
            cornerNE |= CornerFill.Clockwise;
            cornerSE |= CornerFill.CounterClockwise;
        }

        if (counts(se))
            cornerSE |= CornerFill.Diagonal;

        if (counts(s))
        {
            cornerSE |= CornerFill.Clockwise;
            cornerSW |= CornerFill.CounterClockwise;
        }

        if (counts(sw))
            cornerSW |= CornerFill.Diagonal;

        if (counts(w))
        {
            cornerSW |= CornerFill.Clockwise;
            cornerNW |= CornerFill.CounterClockwise;
        }

        if (counts(nw))
            cornerNW |= CornerFill.Diagonal;

        return new CornerSet(cornerNE, cornerNW, cornerSW, cornerSE);
    }

    private static CornerSet Rotate(CornerSet corners, Direction dir)
    {
        var (ne, nw, sw, se) = corners;

        return dir switch
        {
            Direction.North => new CornerSet(sw, se, ne, nw),
            Direction.West => new CornerSet(se, ne, nw, sw),
            Direction.South => new CornerSet(ne, nw, sw, se),
            _ => new CornerSet(nw, sw, se, ne),
        };
    }

    /// <summary>
    /// Returns the strongest join offered by anything anchored on the neighbouring tile.
    /// </summary>
    private EmberWallJoin JoinAt(
        EntityUid uid,
        EmberProceduralWallLayerVisuals visuals,
        AnchoredEntitiesEnumerator candidates,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        var best = EmberWallJoin.None;

        while (candidates.MoveNext(out var entity))
        {
            if (entity == uid)
                continue;

            var join = JoinWith(entity.Value, visuals, wallQuery, structureQuery, doorQuery);

            // Seamless wins over a seam, which wins over nothing: Bay stops at the first blend match per tile,
            // but taking the strongest keeps the result independent of anchored-entity ordering.
            if (join > best)
                best = join;

            if (best == EmberWallJoin.Seamless)
                break;
        }

        return best;
    }

    private EmberWallJoin JoinWith(
        EntityUid entity,
        EmberProceduralWallLayerVisuals visuals,
        EntityQuery<EmberProceduralWallComponent> wallQuery,
        EntityQuery<EmberProceduralStructureComponent> structureQuery,
        EntityQuery<DoorComponent> doorQuery)
    {
        if (_tag.HasTag(entity, NoBlendTag))
            return EmberWallJoin.None;

        if (structureQuery.TryGetComponent(entity, out var structure))
        {
            return EmberProceduralWallBlending.ClassifyStructure(
                structure.Role == EmberProceduralStructureRole.WallFrame
                    ? EmberStructureBlend.Full
                    : EmberStructureBlend.Edge);
        }

        if (wallQuery.TryGetComponent(entity, out var other) &&
            _prototype.TryIndex(other.Material, out EmberWallMaterialPrototype? otherMaterial))
        {
            var otherVisuals = EmberProceduralWallVisuals.Resolve(
                other,
                otherMaterial,
                ResolvePhysical(otherMaterial));

            return EmberProceduralWallBlending.Classify(
                visuals.SmoothKey,
                visuals.BlendKeys,
                visuals.PaintColor,
                otherVisuals.SmoothKey,
                otherVisuals.PaintColor);
        }

        // Doors are on Bay's blend list but not its full-blend list, so they always take a seam.
        return doorQuery.HasComponent(entity) ? EmberWallJoin.Edge : EmberWallJoin.None;
    }

    private EmberMaterialPrototype? ResolvePhysical(EmberWallMaterialPrototype material)
    {
        if (material.PhysicalMaterial is not { } id)
            return null;

        return _prototype.TryIndex(id, out EmberMaterialPrototype? physical) ? physical : null;
    }

    private static void ApplyColors(SpriteComponent sprite, EmberProceduralWallLayerVisuals visuals)
    {
        SetLayerGroup(sprite, BaseLayers, visuals.BaseColor, true);
        SetLayerGroup(sprite, PaintLayers, visuals.PaintColor ?? Color.White, visuals.PaintColor != null);
        SetLayerGroup(sprite, StripeLayers, visuals.StripeColor ?? Color.White, visuals.StripeColor != null);
        SetLayerGroup(sprite, ReinforcementLayers, visuals.ReinforcementColor ?? Color.White, visuals.ReinforcementColor != null);
        SetLayerGroup(sprite, EdgeLayers, visuals.EdgeColor, visuals.HasEdges);
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
            WallLayerKind.Edge => EmberProceduralWallStates.Other(visuals.StateBase, corner, visuals.HasEdges),
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

    private static readonly EmberWallLayer[] EdgeLayers =
    [
        EmberWallLayer.EdgeSE,
        EmberWallLayer.EdgeNE,
        EmberWallLayer.EdgeNW,
        EmberWallLayer.EdgeSW,
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
        new(EmberWallLayer.EdgeSE, DirectionOffset.None, WallLayerKind.Edge),
        new(EmberWallLayer.EdgeNE, DirectionOffset.CounterClockwise, WallLayerKind.Edge),
        new(EmberWallLayer.EdgeNW, DirectionOffset.Flip, WallLayerKind.Edge),
        new(EmberWallLayer.EdgeSW, DirectionOffset.Clockwise, WallLayerKind.Edge),
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
        EdgeSE,
        EdgeNE,
        EdgeNW,
        EdgeSW,
    }

    private enum WallLayerKind : byte
    {
        Base,
        Paint,
        Stripe,
        Reinforcement,
        Edge,
    }

    private readonly record struct CornerSet(CornerFill NE, CornerFill NW, CornerFill SW, CornerFill SE);

    private readonly record struct WallConnections(CornerSet Wall, CornerSet Other);

    [Flags]
    private enum CornerFill : byte
    {
        None = 0,
        CounterClockwise = 1,
        Diagonal = 2,
        Clockwise = 4,
    }
}
