using System.Linq;
using System.Numerics;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.Ember.Structures;

/// <summary>
/// Draws Bay's tables: a frame, its plating, the plating's reinforcement and any felt, each as its own set of
/// four corners that smooth into the tables around it.
/// </summary>
public sealed class EmberProceduralTableSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> DirectionalTag = "Directional";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private readonly Queue<EntityUid> _dirty = new();
    private readonly Queue<EntityUid> _anchorChanged = new();
    private int _generation;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralTableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralTableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmberProceduralTableComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralTableComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(EntityUid uid, EmberProceduralTableComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        if (xform.Anchored && TryComp<MapGridComponent>(xform.GridUid, out var grid))
            component.LastPosition = (xform.GridUid.Value, grid.TileIndicesFor(xform.Coordinates));

        if (TryComp<SpriteComponent>(uid, out var sprite))
            component.UprightDrawDepth ??= sprite.DrawDepth;

        SetupLayers(uid, component);
        DirtyNeighbours(uid, component);
    }

    private void OnShutdown(EntityUid uid, EmberProceduralTableComponent component, ComponentShutdown args)
    {
        DirtyNeighbours(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralTableComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Detaching)
        {
            DirtyNeighbours(uid, component);
            return;
        }

        _anchorChanged.Enqueue(uid);
    }

    private void OnAfterAutoHandleState(EntityUid uid, EmberProceduralTableComponent component, ref AfterAutoHandleStateEvent args)
    {
        DirtyNeighbours(uid, component);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var tables = GetEntityQuery<EmberProceduralTableComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        while (_anchorChanged.TryDequeue(out var uid))
        {
            if (tables.TryGetComponent(uid, out var table))
                DirtyNeighbours(uid, table);
        }

        if (_dirty.Count == 0)
            return;

        _generation++;
        var sprites = GetEntityQuery<SpriteComponent>();

        while (_dirty.TryDequeue(out var uid))
        {
            UpdateSprite(uid, sprites, tables, xforms);
        }
    }

    private void SetupLayers(EntityUid uid, EmberProceduralTableComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetVisible(i, false);
        }

        // Added bottom upwards, so the frame shows through glass plating and the felt covers everything.
        foreach (var kind in Enum.GetValues<EmberTableLayerKind>())
        {
            for (var corner = 0; corner < AllLayers.Length; corner++)
            {
                var layer = LayerFor(kind, corner);
                sprite.LayerMapRemove(layer);
                var index = sprite.AddLayer(new SpriteSpecifier.Rsi(component.Sprite, "frame"));
                sprite.LayerMapSet(layer, index);
                sprite.LayerSetDirOffset(layer, AllLayers[corner].Offset);
                sprite.LayerSetVisible(layer, false);
            }
        }
    }

    private void UpdateSprite(
        EntityUid uid,
        EntityQuery<SpriteComponent> sprites,
        EntityQuery<EmberProceduralTableComponent> tables,
        EntityQuery<TransformComponent> xforms)
    {
        if (!tables.TryGetComponent(uid, out var component) ||
            component.UpdateGeneration == _generation ||
            !sprites.TryGetComponent(uid, out var sprite) ||
            !xforms.TryGetComponent(uid, out var xform))
        {
            return;
        }

        component.UpdateGeneration = _generation;
        SetDrawDepth(sprite, component);
        sprite.EnableDirectionOverride = false;

        EmberMaterialPrototype? plating = null;
        if (component.Material is { } materialId)
            _prototype.TryIndex(materialId, out plating);

        EmberMaterialPrototype? reinforcement = null;
        if (component.Reinforcement is { } reinforcementId)
            _prototype.TryIndex(reinforcementId, out reinforcement);

        MapGridComponent? grid = null;
        if (xform.Anchored)
            TryComp(xform.GridUid, out grid);

        if (component.Flipped)
        {
            DrawFlipped(sprite, component, plating, reinforcement, grid, xform, tables, xforms);
            return;
        }

        var corners = grid == null
            ? default
            : CalculateCorners(uid, grid, component, xform, tables);

        DrawGroup(sprite, component, EmberTableLayerKind.Frame, null, Color.White, corners);

        if (plating != null)
        {
            DrawGroup(sprite, component, EmberTableLayerKind.Plating,
                EmberProceduralTableVisuals.PlatingStateBase(plating), ColorOf(plating), corners);
        }
        else
        {
            HideGroup(sprite, EmberTableLayerKind.Plating);
        }

        if (plating != null && reinforcement != null)
        {
            DrawGroup(sprite, component, EmberTableLayerKind.Reinforcement,
                EmberProceduralTableVisuals.ReinforcementStateBase(plating), ColorOf(reinforcement), corners);
        }
        else
        {
            HideGroup(sprite, EmberTableLayerKind.Reinforcement);
        }

        if (component.Carpeted)
        {
            DrawGroup(sprite, component, EmberTableLayerKind.Carpet,
                EmberProceduralTableVisuals.CarpetStateBase, Color.White, corners);
        }
        else
        {
            HideGroup(sprite, EmberTableLayerKind.Carpet);
        }
    }

    /// <summary>
    /// A table on its side has to be able to draw both behind somebody sheltering behind it and in front of
    /// somebody standing on the near side. The engine already does exactly that for anything sharing a draw
    /// depth: within one depth it sorts by where the sprite sits on screen. So a flipped table joins the mobs'
    /// depth and lets that sorting decide, which stays right while a player walks past rather than stepping.
    /// </summary>
    private static void SetDrawDepth(SpriteComponent sprite, EmberProceduralTableComponent component)
    {
        var depth = component.Flipped
            ? (int) Content.Shared.DrawDepth.DrawDepth.Mobs
            : component.UprightDrawDepth ?? (int) Content.Shared.DrawDepth.DrawDepth.Objects;

        if (sprite.DrawDepth != depth)
            sprite.DrawDepth = depth;
    }

    /// <summary>
    /// A flipped table is one sprite rather than four corners, picked by how many of its neighbours are lying
    /// the same way.
    /// </summary>
    private void DrawFlipped(
        SpriteComponent sprite,
        EmberProceduralTableComponent component,
        EmberMaterialPrototype? plating,
        EmberMaterialPrototype? reinforcement,
        MapGridComponent? grid,
        TransformComponent xform,
        EntityQuery<EmberProceduralTableComponent> tables,
        EntityQuery<TransformComponent> xforms)
    {
        var facing = component.FlipFacing;

        // The frame to draw is chosen outright rather than by turning the entity, so that what you see and what
        // you walk into are decided by the same networked value.
        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = facing;

        var run = "0";

        if (grid != null)
        {
            var pos = grid.TileIndicesFor(xform.Coordinates);
            run = EmberProceduralTableVisuals.FlippedRun(
                HasFlippedNeighbour(grid, pos, Rotate(facing, true), component, facing, tables, xforms),
                HasFlippedNeighbour(grid, pos, Rotate(facing, false), component, facing, tables, xforms));
        }

        SetSingle(sprite, component, EmberTableLayerKind.Frame, null, run, Color.White);
        SetSingle(sprite, component, EmberTableLayerKind.Plating,
            plating == null ? null : EmberProceduralTableVisuals.PlatingStateBase(plating), run,
            plating == null ? Color.White : ColorOf(plating), plating != null);
        SetSingle(sprite, component, EmberTableLayerKind.Reinforcement,
            plating == null ? null : EmberProceduralTableVisuals.ReinforcementStateBase(plating), run,
            reinforcement == null ? Color.White : ColorOf(reinforcement), plating != null && reinforcement != null);
        SetSingle(sprite, component, EmberTableLayerKind.Carpet,
            EmberProceduralTableVisuals.CarpetStateBase, run, Color.White, component.Carpeted);
    }

    private void SetSingle(
        SpriteComponent sprite,
        EmberProceduralTableComponent component,
        EmberTableLayerKind kind,
        string? stateBase,
        string run,
        Color color,
        bool visible = true)
    {
        HideGroup(sprite, kind);

        if (!visible)
            return;

        // Only some materials were ever drawn lying down; the rest fall back on the bare frame's shape, which is
        // still the right silhouette to tint.
        var state = EmberProceduralTableVisuals.FlippedState(stateBase, run);
        if (!HasState(component, state))
            state = EmberProceduralTableVisuals.FlippedState(null, run);

        var layer = LayerFor(kind, 0);
        sprite.LayerSetState(layer, state);
        sprite.LayerSetColor(layer, color);
        sprite.LayerSetDirOffset(layer, DirectionOffset.None);
        sprite.LayerSetVisible(layer, true);
    }

    private void DrawGroup(
        SpriteComponent sprite,
        EmberProceduralTableComponent component,
        EmberTableLayerKind kind,
        string? stateBase,
        Color color,
        EmberProceduralStructureLayerCorners<int> corners)
    {
        var values = new[] { corners.SE, corners.NE, corners.NW, corners.SW };

        for (var i = 0; i < AllLayers.Length; i++)
        {
            var layer = LayerFor(kind, i);
            var state = EmberProceduralTableVisuals.CornerState(stateBase, values[i]);

            if (!HasState(component, state))
            {
                sprite.LayerSetVisible(layer, false);
                continue;
            }

            sprite.LayerSetState(layer, state);
            sprite.LayerSetColor(layer, color);
            sprite.LayerSetDirOffset(layer, AllLayers[i].Offset);
            sprite.LayerSetVisible(layer, true);
        }
    }

    private static void HideGroup(SpriteComponent sprite, EmberTableLayerKind kind)
    {
        for (var i = 0; i < AllLayers.Length; i++)
        {
            sprite.LayerSetVisible(LayerFor(kind, i), false);
        }
    }

    /// <summary>
    /// Not every material was drawn for every layer — there is no stone reinforcement and no stone lying on its
    /// side — so a state that was never drawn is asked about rather than assumed.
    /// </summary>
    private bool HasState(EmberProceduralTableComponent component, string state)
    {
        return _resource.GetResource<RSIResource>(component.Sprite).RSI.TryGetState(state, out _);
    }

    /// <summary>
    /// The colour and the see-through-ness both come from the material, which is what makes a glass table a
    /// glass table and lets the frame show through it.
    /// </summary>
    private static Color ColorOf(EmberMaterialPrototype material)
    {
        return material.Color.WithAlpha(Math.Clamp(material.Opacity, 0f, 1f));
    }

    private EmberProceduralStructureLayerCorners<int> CalculateCorners(
        EntityUid uid,
        MapGridComponent grid,
        EmberProceduralTableComponent component,
        TransformComponent xform,
        EntityQuery<EmberProceduralTableComponent> tables)
    {
        var pos = grid.TileIndicesFor(xform.Coordinates);
        var blocked = BlockedDirections(grid, pos);

        bool Joined(Direction direction)
        {
            if ((blocked & ToFlag(direction)) != 0)
                return false;

            var candidates = grid.GetAnchoredEntitiesEnumerator(pos.Offset(direction));

            while (candidates.MoveNext(out var candidate))
            {
                if (candidate == uid)
                    continue;

                if (tables.TryGetComponent(candidate.Value, out var other) &&
                    EmberProceduralTableVisuals.Joins(component, other))
                {
                    return true;
                }
            }

            return false;
        }

        var n = Joined(Direction.North);
        var e = Joined(Direction.East);
        var s = Joined(Direction.South);
        var w = Joined(Direction.West);

        // Which neighbour is the anticlockwise one depends on the corner: for the north-east corner that is
        // north and the clockwise one is east, and so on round. Getting the pair the wrong way round still
        // produces a plausible-looking number for every corner, which is why it has to be read off carefully
        // rather than eyeballed.
        var se = Corner(e, Joined(Direction.SouthEast), s);
        var ne = Corner(n, Joined(Direction.NorthEast), e);
        var nw = Corner(w, Joined(Direction.NorthWest), n);
        var sw = Corner(s, Joined(Direction.SouthWest), w);

        return EmberProceduralStructureCorners.MapToLayers(
            xform.LocalRotation.GetCardinalDir(), se, ne, nw, sw);
    }

    private static int Corner(bool counterClockwise, bool diagonal, bool clockwise)
    {
        return (counterClockwise ? 1 : 0) | (diagonal ? 2 : 0) | (clockwise ? 4 : 0);
    }

    /// <summary>
    /// Bay stops a table from reaching through a window: a full-tile one on the table's own tile cuts it off
    /// entirely, and a pane in the way blocks the direction it stands in, along with the diagonals either side.
    /// </summary>
    private byte BlockedDirections(MapGridComponent grid, Vector2i pos)
    {
        byte blocked = 0;

        var here = grid.GetAnchoredEntitiesEnumerator(pos);
        while (here.MoveNext(out var candidate))
        {
            if (!_tag.HasTag(candidate.Value, WindowTag))
                continue;

            if (!_tag.HasTag(candidate.Value, DirectionalTag))
                return 0xFF;

            blocked |= ToFlag(Transform(candidate.Value).LocalRotation.GetCardinalDir());
        }

        foreach (var direction in Cardinals)
        {
            var neighbours = grid.GetAnchoredEntitiesEnumerator(pos.Offset(direction));

            while (neighbours.MoveNext(out var candidate))
            {
                if (!_tag.HasTag(candidate.Value, WindowTag))
                    continue;

                if (!_tag.HasTag(candidate.Value, DirectionalTag) ||
                    Transform(candidate.Value).LocalRotation.GetCardinalDir() == Opposite(direction))
                {
                    blocked |= ToFlag(direction);
                    break;
                }
            }
        }

        // A blocked cardinal takes the diagonals beside it with it, or the corner sprites disagree with the
        // sides and the table grows a notch.
        foreach (var (diagonal, first, second) in Diagonals)
        {
            if ((blocked & ToFlag(first)) != 0 || (blocked & ToFlag(second)) != 0)
                blocked |= ToFlag(diagonal);
        }

        return blocked;
    }

    private bool HasFlippedNeighbour(
        MapGridComponent grid,
        Vector2i pos,
        Direction direction,
        EmberProceduralTableComponent component,
        Direction facing,
        EntityQuery<EmberProceduralTableComponent> tables,
        EntityQuery<TransformComponent> xforms)
    {
        var candidates = grid.GetAnchoredEntitiesEnumerator(pos.Offset(direction));

        while (candidates.MoveNext(out var candidate))
        {
            if (tables.TryGetComponent(candidate.Value, out var other) &&
                EmberProceduralTableVisuals.Joins(component, other) &&
                other.FlipFacing == facing)
            {
                return true;
            }
        }

        return false;
    }

    private void DirtyNeighbours(EntityUid uid, EmberProceduralTableComponent component)
    {
        _dirty.Enqueue(uid);

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        Vector2i pos;
        MapGridComponent? grid;

        if (xform.Anchored && TryComp(xform.GridUid, out grid))
        {
            pos = grid.TileIndicesFor(xform.Coordinates);
            component.LastPosition = (xform.GridUid.Value, pos);
        }
        else
        {
            if (component.LastPosition is not (EntityUid gridId, Vector2i oldPos) || !TryComp(gridId, out grid))
                return;

            pos = oldPos;
        }

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var entities = grid.GetAnchoredEntitiesEnumerator(pos + new Vector2i(x, y));

                while (entities.MoveNext(out var entity))
                    _dirty.Enqueue(entity.Value);
            }
        }
    }

    private static byte ToFlag(Direction direction) => (byte) (1 << (int) direction);

    private static Direction Opposite(Direction direction) => (Direction) (((int) direction + 4) % 8);

    private static Direction Rotate(Direction direction, bool clockwise)
    {
        return (Direction) (((int) direction + (clockwise ? 2 : 6)) % 8);
    }

    private static EmberTableLayer LayerFor(EmberTableLayerKind kind, int corner)
    {
        return (EmberTableLayer) ((int) kind * 4 + corner);
    }

    private static readonly Direction[] Cardinals =
    [
        Direction.North, Direction.East, Direction.South, Direction.West,
    ];

    private static readonly (Direction Diagonal, Direction First, Direction Second)[] Diagonals =
    [
        (Direction.NorthEast, Direction.North, Direction.East),
        (Direction.SouthEast, Direction.South, Direction.East),
        (Direction.SouthWest, Direction.South, Direction.West),
        (Direction.NorthWest, Direction.North, Direction.West),
    ];

    private static readonly TableLayerDefinition[] AllLayers =
    [
        new(DirectionOffset.None),
        new(DirectionOffset.CounterClockwise),
        new(DirectionOffset.Flip),
        new(DirectionOffset.Clockwise),
    ];

    private readonly record struct TableLayerDefinition(DirectionOffset Offset);

    private enum EmberTableLayer : byte
    {
        FrameSE,
        FrameNE,
        FrameNW,
        FrameSW,
        PlatingSE,
        PlatingNE,
        PlatingNW,
        PlatingSW,
        ReinforcementSE,
        ReinforcementNE,
        ReinforcementNW,
        ReinforcementSW,
        CarpetSE,
        CarpetNE,
        CarpetNW,
        CarpetSW,
    }
}
