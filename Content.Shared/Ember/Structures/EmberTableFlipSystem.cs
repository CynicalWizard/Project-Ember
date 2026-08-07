using System.Numerics;
using Content.Shared.Climbing.Components;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Structures;

/// <summary>
/// Bay lets you tip a table onto its side to hide behind. It stops being something to put things on and becomes
/// a lip along one edge: cover to shoot over, and no longer a surface to climb.
/// </summary>
public sealed class EmberTableFlipSystem : EntitySystem
{
    private const float FlipSeconds = 1f;
    private const string FixtureId = "fix1";

    /// <summary>How far a row of tables is followed before giving up on deciding whether it is straight.</summary>
    private const int MaximumRun = 32;


    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralTableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<EmberProceduralTableComponent, EmberTableFlipDoAfterEvent>(OnFlipped);
    }

    private void OnGetVerbs(Entity<EmberProceduralTableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        var flipped = ent.Comp.Flipped;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStart(ent, user),
            Text = Loc.GetString(flipped ? "ember-table-verb-unflip" : "ember-table-verb-flip"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
        });
    }

    private void TryStart(Entity<EmberProceduralTableComponent> ent, EntityUid user)
    {
        if (!TryGetFacing(ent, user, out var facing))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            FlipSeconds,
            new EmberTableFlipDoAfterEvent(GetNetEntity(ent)),
            ent,
            ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnFlipped(Entity<EmberProceduralTableComponent> ent, ref EmberTableFlipDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Fixtures are the server's to change. The client used to predict the swap, and since a table's collision
        // is its whole point, a mispredicted one let you walk into a table the server then shoved you back out of.
        if (!_net.IsServer)
            return;

        if (!TryGetFacing(ent, args.User, out var facing))
            return;

        SetFlipped(ent, facing, !ent.Comp.Flipped, args.User);
    }

    /// <summary>
    /// Which way this table can actually be pushed from where the user is standing, if it can be pushed at all.
    /// </summary>
    /// <remarks>
    /// Bay only ever has to consider one answer, because on a tile grid you are squarely on one side. Standing
    /// at a corner is normal for us, so both edges nearest the user are fair readings of "away from me" and the
    /// one that works is taken. Without that, a table you are standing diagonally off refuses for a reason the
    /// player has no way to see.
    /// </remarks>
    public bool TryGetFacing(Entity<EmberProceduralTableComponent> ent, EntityUid user, out Direction facing)
    {
        facing = ent.Comp.FlipFacing;

        if (ent.Comp.Material == null)
        {
            _popup.PopupClient(Loc.GetString("ember-table-flip-bare"), user, user);
            return false;
        }

        if (ent.Comp.Flipped)
            return CanStandBackUp(ent, user);

        if (ent.Comp.Reinforcement != null)
        {
            _popup.PopupClient(Loc.GetString("ember-table-flip-reinforced"), user, user);
            return false;
        }

        var edges = NearestEdges(user, ent);

        if (edges.Length == 0)
        {
            _popup.PopupClient(Loc.GetString("ember-table-flip-underfoot"), user, user);
            return false;
        }

        foreach (var candidate in edges)
        {
            if (!IsRowStraight(ent, candidate))
                continue;

            facing = candidate;
            return true;
        }

        _popup.PopupClient(Loc.GetString("ember-table-flip-blocked"), user, user);
        return false;
    }

    /// <summary>
    /// The edge the user is on: which way the table goes over is which way it is being pushed, away from them.
    /// </summary>
    /// <remarks>
    /// Measured tile to tile, not point to point. A player does not stand on the middle of their tile, and
    /// comparing two positions makes the answer depend on where in the tile they happened to stop: anywhere near
    /// the line running through the table, a few pixels either way swings it between two edges, and standing on
    /// the table itself it swings between all four. That is a table that goes over the right way, the wrong way
    /// or refuses depending on nothing the player can see. Bay never has the problem because it asks which turf
    /// you are on, so that is what this asks.
    ///
    /// Standing diagonally offers both edges nearest the user, since either is a fair reading of which side they
    /// are on, and the row check decides between them. Standing on the table offers none: there is no away.
    /// </remarks>
    private Direction[] NearestEdges(EntityUid user, EntityUid table)
    {
        var (dx, dy) = TileOffset(user, table);

        if (dx == 0 && dy == 0)
            return [];

        var horizontal = dx > 0 ? Direction.East : Direction.West;
        var vertical = dy > 0 ? Direction.North : Direction.South;

        if (dy == 0)
            return [horizontal];

        if (dx == 0)
            return [vertical];

        // A corner: both are away from the user, so the one they are further along comes first. Standing square
        // on the diagonal there is nothing to choose between them, and the tie goes to the vertical — not
        // because it is better but because it has to be the same answer every time. Measuring the leftover
        // fraction of a tile to break it is what made the whole thing unpredictable in the first place.
        if (Math.Abs(dx) > Math.Abs(dy))
            return [horizontal, vertical];

        return [vertical, horizontal];
    }

    /// <summary>How many tiles the table is from the user, along each axis.</summary>
    private (int X, int Y) TileOffset(EntityUid user, EntityUid table)
    {
        var tableXform = Transform(table);

        if (tableXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return WorldOffset();

        var userXform = Transform(user);

        // Off the grid the table is on, there are no shared tiles to compare.
        if (userXform.GridUid != gridUid)
            return WorldOffset();

        var here = grid.TileIndicesFor(tableXform.Coordinates);
        var there = grid.TileIndicesFor(userXform.Coordinates);

        return (here.X - there.X, here.Y - there.Y);

        (int X, int Y) WorldOffset()
        {
            var away = _transform.GetWorldPosition(table) - _transform.GetWorldPosition(user);
            return (MathF.Sign(away.X), MathF.Sign(away.Y));
        }
    }

    private bool CanStandBackUp(Entity<EmberProceduralTableComponent> ent, EntityUid user)
    {
        // Standing it back up would put the tabletop where somebody is.
        foreach (var occupant in Transform(ent).Coordinates.GetEntitiesInTile(LookupFlags.Uncontained))
        {
            if (occupant != ent.Owner && HasComp<MobStateComponent>(occupant))
            {
                _popup.PopupClient(Loc.GetString("ember-table-unflip-occupied"), user, user);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Bay only flips a single row of tables at a time: a block two deep would leave half of it standing, so it
    /// refuses rather than making a mess.
    /// </summary>
    private bool IsRowStraight(Entity<EmberProceduralTableComponent> ent, Direction facing)
    {
        return IsStraightRun(ent, Rotate(facing, true)) && IsStraightRun(ent, Rotate(facing, false));
    }

    /// <summary>
    /// Walks along a row of tables of the same material, refusing if anything of that material stands off to
    /// either side of it. Bay recurses; the run is followed with a step limit instead, since a grid can be big.
    /// </summary>
    private bool IsStraightRun(Entity<EmberProceduralTableComponent> ent, Direction direction)
    {
        var xform = Transform(ent);

        if (!xform.Anchored || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return true;

        var pos = grid.TileIndicesFor(xform.Coordinates);

        for (var step = 0; step < MaximumRun; step++)
        {
            foreach (var side in new[] { Rotate(direction, true), Rotate(direction, false) })
            {
                if (FindTable(grid, pos.Offset(side), ent.Comp) is { Flipped: false })
                    return false;
            }

            var next = FindTable(grid, pos.Offset(direction), ent.Comp);

            if (next is not { Flipped: false })
                return true;

            pos = pos.Offset(direction);
        }

        return true;
    }

    /// <summary>
    /// Tips the table over, or stands it back up, along with the rest of its row. Public so the effects of it —
    /// the lip it leaves behind, and no longer being something to climb — can be asserted rather than assumed.
    /// </summary>
    public void SetFlipped(Entity<EmberProceduralTableComponent> ent, Direction facing, bool flip, EntityUid? user = null)
    {
        ent.Comp.Flipped = flip;
        ent.Comp.FlipFacing = facing;
        Dirty(ent);

        SetCover(ent, flip, facing);

        if (flip)
        {
            ThrowOff(ent, facing, user);

            // Bay charges a little damage for tipping it over, which is what stops a table being free cover you
            // can put back up indefinitely.
            _damageable.TryChangeDamage(ent, new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = _random.Next(5, 11) },
            });
        }

        Propagate(ent, facing, flip, user);
    }

    /// <summary>Turns the rest of the row, so a line of tables becomes one barricade rather than a gap-toothed one.</summary>
    private void Propagate(Entity<EmberProceduralTableComponent> ent, Direction facing, bool flip, EntityUid? user)
    {
        var xform = Transform(ent);

        if (!xform.Anchored || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var pos = grid.TileIndicesFor(xform.Coordinates);

        foreach (var side in new[] { Rotate(facing, true), Rotate(facing, false) })
        {
            var here = pos;

            for (var step = 0; step < MaximumRun; step++)
            {
                here = here.Offset(side);

                if (FindTable(grid, here, ent.Comp) is not { } neighbour || neighbour.Flipped == flip)
                    break;

                // Unflipping only reaches tables lying the same way this one was.
                if (!flip && neighbour.FlipFacing != facing)
                    break;

                SetFlipped((neighbour.Owner, neighbour), facing, flip, user);
            }
        }
    }

    private void SetCover(Entity<EmberProceduralTableComponent> ent, bool flipped, Direction facing)
    {
        if (TryComp<FixturesComponent>(ent, out var fixtures) &&
            _fixtures.GetFixtureOrNull(ent, FixtureId, fixtures) is { } fixture)
        {
            var layer = fixture.CollisionLayer;
            var mask = fixture.CollisionMask;
            var density = fixture.Density;

            if (flipped)
            {
                ent.Comp.UprightShape = fixture.Shape;
                ent.Comp.UprightLayer = layer;
            }

            // A hitscan beam is a ray cast against the opaque layer, which a table does not sit on — you shoot
            // over one, after all. On its side it is cover, so it has to be on that layer or lasers go straight
            // through the thing you are hiding behind.
            layer = flipped
                ? layer | (int) CollisionGroup.Opaque
                : ent.Comp.UprightLayer ?? layer;

            _fixtures.DestroyFixture(ent, FixtureId, fixture, manager: fixtures);

            IPhysShape shape;

            if (!flipped && ent.Comp.UprightShape is { } remembered)
            {
                shape = remembered;
            }
            else
            {
                var box = new PolygonShape();
                box.SetAsBox(flipped
                    ? EmberProceduralTableVisuals.LipFor(ent.Comp.FlippedBounds, facing)
                    : ent.Comp.UprightBounds);
                shape = box;
            }

            _fixtures.TryCreateFixture(ent, shape, FixtureId, density,
                collisionLayer: layer, collisionMask: mask, manager: fixtures);

            // Taking the last fixture off a body switches its collision off, and putting one back on does not
            // switch it on again. Without this a table stops colliding with anything the first time it is tipped
            // over and never starts again, however many times you stand it back up.
            _physics.SetCanCollide(ent, true, manager: fixtures);
        }

        if (flipped)
        {
            // On its side it is a barricade: nothing to climb onto, and bullets meet it rather than passing over.
            RemComp<ClimbableComponent>(ent);
            RemComp<RequireProjectileTargetComponent>(ent);
        }
        else
        {
            EnsureComp<ClimbableComponent>(ent);
            EnsureComp<RequireProjectileTargetComponent>(ent);
        }
    }

    private void ThrowOff(Entity<EmberProceduralTableComponent> ent, Direction facing, EntityUid? user)
    {
        var direction = facing.ToVec();

        foreach (var loose in Transform(ent).Coordinates.GetEntitiesInTile(LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (loose == ent.Owner || Transform(loose).Anchored)
                continue;

            _throwing.TryThrow(loose, direction, 3f, user);
        }
    }

    private EmberProceduralTableComponent? FindTable(
        MapGridComponent grid,
        Vector2i pos,
        EmberProceduralTableComponent like)
    {
        var candidates = grid.GetAnchoredEntitiesEnumerator(pos);

        while (candidates.MoveNext(out var candidate))
        {
            if (TryComp<EmberProceduralTableComponent>(candidate.Value, out var table) &&
                table.Material == like.Material)
            {
                return table;
            }
        }

        return null;
    }

    private static Direction Rotate(Direction direction, bool clockwise)
    {
        return (Direction) (((int) direction + (clockwise ? 2 : 6)) % 8);
    }
}

[Serializable, NetSerializable]
public sealed partial class EmberTableFlipDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Table;

    private EmberTableFlipDoAfterEvent()
    {
    }

    public EmberTableFlipDoAfterEvent(NetEntity table)
    {
        Table = table;
    }

    public override DoAfterEvent Clone() => this;
}
