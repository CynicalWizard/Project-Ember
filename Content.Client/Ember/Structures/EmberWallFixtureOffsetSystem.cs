using System.Numerics;
using Content.Shared.Ember.Structures;
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
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly Queue<EntityUid> _dirty = new();

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

        // Which wall is the far one is a question about the screen, not the grid, so turning the camera changes
        // the answer for every fitting at once.
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

        // Everything about a three-quarter view is a statement about the screen. A wall drawn tall is the one
        // above you as you look at it, and it stops being that the moment the camera turns — which it does,
        // and which the walls themselves already follow, since a sprite picks its direction from the angle it
        // has on screen. So both how far to sink and which way that is are asked in screen terms.
        var onScreen = _transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation;
        var facing = onScreen.GetCardinalDir();

        var depth = 0;

        if (xform.Anchored &&
            xform.GridUid is { } gridUid &&
            TryComp<MapGridComponent>(gridUid, out var grid) &&
            ent.Comp.Depth.TryGetValue(facing, out var wanted))
        {
            // The wall itself is where it always was, so that question stays on the grid.
            var behindOnGrid = xform.LocalRotation.GetCardinalDir().GetOpposite();
            var behind = grid.TileIndicesFor(xform.Coordinates).Offset(behindOnGrid);

            if (_turf.IsTileBlocked(gridUid, behind, CollisionGroup.Impassable, grid))
                depth = wanted;
        }

        ent.Comp.Facing = facing;
        ent.Comp.AgainstWall = depth != 0;

        // Up the screen is away from the viewer, which is where the far wall is. The sprite's own frame is
        // turned by the fitting and then by the camera, so that has to come back out of the offset.
        sprite.Offset = (-onScreen).RotateVec(new Vector2(0f, depth / 32f));
    }
}
