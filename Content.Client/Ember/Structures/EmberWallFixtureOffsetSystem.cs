using System.Numerics;
using Content.Shared.Ember.Structures;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

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
/// </remarks>
public sealed class EmberWallFixtureOffsetSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<EmberWallFixtureOffsetComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnStartup(Entity<EmberWallFixtureOffsetComponent> ent, ref ComponentStartup args)
    {
        Update(ent);
    }

    private void OnMoved(Entity<EmberWallFixtureOffsetComponent> ent, ref MoveEvent args)
    {
        Update(ent);
    }

    private void OnAnchorChanged(Entity<EmberWallFixtureOffsetComponent> ent, ref AnchorStateChangedEvent args)
    {
        Update(ent);
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
