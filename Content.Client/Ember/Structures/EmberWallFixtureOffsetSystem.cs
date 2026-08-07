using System.Numerics;
using Content.Shared.Ember.Structures;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client.Ember.Structures;

/// <summary>
/// Pushes a wall fitting into the wall behind it, so it reads as being on that wall rather than floating in
/// front of it, and takes it away again from anyone standing on the far side of that wall.
/// </summary>
/// <remarks>
/// The offset lives in the sprite's own frame, which turns with the fitting, and behind is always straight up
/// in that frame — a fitting at rest faces south, so up is north, which is where its wall is. That is what Bay
/// does too, in pixels: <c>pixel_y = 21</c> for a wall to the north, <c>pixel_x = ±10</c> for one to either
/// side. It moves towards its wall, never merely upwards.
///
/// Only the distance is a question about the screen. Walls are drawn tall, so the one across the top of the
/// view shows a whole face to climb, one at the side shows an edge, and one along the bottom shows nothing —
/// and which of those a given wall is changes the moment the camera turns, since a sprite picks its direction
/// from the angle it has on screen. The direction to move needs no such thought: the wall does not go anywhere
/// when the view spins, and neither does the sprite frame, so the two keep step on their own.
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
    /// <summary>How far from the camera to bother asking, comfortably past the edge of any screen.</summary>
    private const float Sight = 32f;

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly Queue<EntityUid> _dirty = new();

    private readonly HashSet<Entity<EmberWallFixtureOffsetComponent>> _nearby = new();
    private readonly HashSet<EntityUid> _outOfSight = new();
    private readonly HashSet<EntityUid> _stale = new();

    private EntityQuery<EmberWallFixtureOffsetComponent> _query;
    private Angle _lastEyeRotation;

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

        // How far to sink is a question about the screen, so turning the camera changes the answer for every
        // fitting at once.
        var eye = _eye.CurrentEye.Rotation;

        if (!eye.EqualsApprox(_lastEyeRotation))
        {
            _lastEyeRotation = eye;

            var all = AllEntityQuery<EmberWallFixtureOffsetComponent>();
            while (all.MoveNext(out var uid, out _))
            {
                _dirty.Enqueue(uid);
            }
        }

        while (_dirty.TryDequeue(out var uid))
        {
            if (_query.TryGetComponent(uid, out var component))
                Update((uid, component));
        }

        HideWhatIsBehindAWall();
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

        // A wall drawn tall is the one above you as you look at it, and it stops being that the moment the
        // camera turns — which the walls themselves already follow, since a sprite picks its direction from the
        // angle it has on screen. So how deep a wall is worth is asked in screen terms.
        var facing = (_transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation).GetCardinalDir();

        var depth = 0;

        if (xform.Anchored &&
            xform.GridUid is { } gridUid &&
            TryComp<MapGridComponent>(gridUid, out var grid) &&
            ent.Comp.Depth.TryGetValue(facing, out var wanted))
        {
            // Where the wall is, on the other hand, is a question for the grid: it does not move when the view
            // spins, and neither does the sprite frame this offset is written in.
            var behindOnGrid = xform.LocalRotation.GetCardinalDir().GetOpposite();
            var behind = grid.TileIndicesFor(xform.Coordinates).Offset(behindOnGrid);

            if (_turf.IsTileBlocked(gridUid, behind, CollisionGroup.Impassable, grid))
                depth = wanted;
        }

        ent.Comp.Facing = facing;
        ent.Comp.AgainstWall = depth != 0;

        // Up in the sprite's own frame is behind the fitting, which is where its wall is.
        _sprite.SetOffset((ent.Owner, sprite), new Vector2(0f, depth / 32f));
    }

    /// <summary>
    /// A fitting sunk into a wall has left its own tile, and the wall tile it landed on is one both rooms can
    /// see. Bay never has to think about this: BYOND does not draw an object whose turf you cannot see, and the
    /// turf is the one in the room the light belongs to. Ours is drawn regardless, so a light in a sealed room
    /// shows up on the far face of its wall to anyone walking past outside. This puts that back.
    /// </summary>
    private void HideWhatIsBehindAWall()
    {
        var eye = _eye.CurrentEye.Position;

        _stale.Clear();
        _stale.UnionWith(_outOfSight);

        _nearby.Clear();
        if (eye.MapId != MapId.Nullspace)
            _lookup.GetEntitiesInRange(eye, Sight, _nearby);

        foreach (var ent in _nearby)
        {
            _stale.Remove(ent.Owner);
            Show(ent, !ent.Comp.AgainstWall || CanBeSeenFrom(ent, eye));
        }

        // Anything hidden that has since fallen out of range would never be asked again, and would stay a hole
        // in the wall for as long as the round lasted.
        foreach (var uid in _stale)
        {
            if (_query.TryGetComponent(uid, out var component))
                Show((uid, component), true);
            else
                _outOfSight.Remove(uid);
        }
    }

    private bool CanBeSeenFrom(Entity<EmberWallFixtureOffsetComponent> ent, MapCoordinates eye)
    {
        var xform = Transform(ent);
        var here = _transform.GetMapCoordinates(ent.Owner, xform);

        if (here.MapId != eye.MapId)
            return true;

        // The only thing that can hide it is the wall it leans into, and only from the far side of that wall.
        // Asking that first is a dot product, and it comes out false for very nearly every fitting on screen,
        // which is what keeps the ray below rare.
        var behind = _transform.GetWorldRotation(xform).RotateVec(new Vector2(0f, 1f));

        if (Vector2.Dot(eye.Position - (here.Position + behind), behind) <= 0f)
            return true;

        return _examine.InRangeUnOccluded(eye, here, Sight, null);
    }

    private void Show(Entity<EmberWallFixtureOffsetComponent> ent, bool visible)
    {
        if (ent.Comp.Shown == visible)
            return;

        ent.Comp.Shown = visible;

        if (visible)
            _outOfSight.Remove(ent.Owner);
        else
            _outOfSight.Add(ent.Owner);

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent.Owner, sprite), visible);
    }
}
