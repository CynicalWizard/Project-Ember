using System.Numerics;
using Content.Shared.Ember.Structures;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client.Ember.Structures;

/// <summary>
/// Pushes a wall fitting into the wall behind it, so it reads as being on that wall rather than floating in
/// front of it.
/// </summary>
/// <remarks>
/// The offset lives in the sprite's own frame, which turns with the fitting, and behind is always straight up
/// in that frame — a fitting at rest faces south, so up is north, which is where its wall is. That means only
/// the distance has to be worked out per direction; the direction takes care of itself.
///
/// Nothing happens unless the tile behind is actually blocked, which is Bay's rule and matters: a light on a
/// pole in the middle of a room would otherwise stand a fifth of a tile away from where it is.
///
/// The work is queued rather than done on the spot because of what arrives when. A fitting loaded off a map is
/// built before the wall it hangs on, so asking at that moment gets the answer "open floor" and the fitting
/// never moves; one a player builds is dropped into a world that already has its wall, and looks right. That is
/// exactly the difference between a station's own lights and a freshly placed one, and it is why this waits a
/// frame before deciding. Walls also call in when they change, so building or breaking one moves whatever hangs
/// beside it.
/// </remarks>
public sealed class EmberWallFixtureOffsetSystem : EntitySystem
{
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly Queue<EntityUid> _dirty = new();

    private EntityQuery<EmberWallFixtureOffsetComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<EmberWallFixtureOffsetComponent>();

        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnStartup(Entity<EmberWallFixtureOffsetComponent> ent, ref ComponentStartup args)
    {
        _dirty.Enqueue(ent);
    }

    private void OnMoved(Entity<EmberWallFixtureOffsetComponent> ent, ref MoveEvent args)
    {
        _dirty.Enqueue(ent);
    }

    private void OnAnchorChanged(Entity<EmberWallFixtureOffsetComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty.Enqueue(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        while (_dirty.TryDequeue(out var uid))
        {
            if (_query.TryGetComponent(uid, out var component))
                Update((uid, component));
        }
    }

    /// <summary>Whatever hangs on the four tiles around this one has to look again.</summary>
    public void DirtyAround(MapGridComponent grid, Vector2i pos)
    {
        foreach (var direction in new[] { Direction.North, Direction.South, Direction.East, Direction.West })
        {
            Enqueue(grid.GetAnchoredEntitiesEnumerator(pos.Offset(direction)));
        }
    }

    private void Enqueue(AnchoredEntitiesEnumerator entities)
    {
        while (entities.MoveNext(out var entity))
        {
            if (_query.HasComponent(entity.Value))
                _dirty.Enqueue(entity.Value);
        }
    }

    public void Update(Entity<EmberWallFixtureOffsetComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var xform = Transform(ent);
        var facing = xform.LocalRotation.GetCardinalDir();

        var depth = 0;

        if (xform.Anchored &&
            xform.GridUid is { } gridUid &&
            TryComp<MapGridComponent>(gridUid, out var grid) &&
            ent.Comp.Depth.TryGetValue(facing, out var wanted))
        {
            var behind = grid.TileIndicesFor(xform.Coordinates).Offset(facing.GetOpposite());

            if (_turf.IsTileBlocked(gridUid, behind, CollisionGroup.Impassable, grid))
                depth = wanted;
        }

        ent.Comp.Facing = facing;
        ent.Comp.AgainstWall = depth != 0;

        // Straight up in the sprite's own frame is straight into the wall behind, whichever wall that is.
        sprite.Offset = new Vector2(0f, depth / 32f);
    }
}
