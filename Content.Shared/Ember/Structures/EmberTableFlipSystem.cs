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

    /// <summary>How far off to one side counts as standing at a corner rather than squarely on one side.</summary>
    private const float CornerFraction = 0.4f;

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

        foreach (var candidate in NearestEdges(user, ent))
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
    /// The edge the user is on. Standing at a corner offers both edges nearest them, since either is a fair
    /// reading of which side they are on; standing squarely on one side offers only that one.
    /// </summary>
    /// <remarks>
    /// The difference matters. Offering a second edge to somebody standing squarely at the end of a long row
    /// means the row refuses to go over that way — correctly, it is a row — and the table quietly goes over
    /// sideways instead. From the player's side of the screen that reads as the table falling in a direction
    /// they did not choose, and as no rule at all once they try it from a few different places.
    /// </remarks>
    private Direction[] NearestEdges(EntityUid user, EntityUid table)
    {
        var away = _transform.GetWorldPosition(table) - _transform.GetWorldPosition(user);
        var primary = EmberProceduralTableVisuals.FlipDirection(
            _transform.GetWorldPosition(user), _transform.GetWorldPosition(table));

        var along = MathF.Abs(primary is Direction.North or Direction.South ? away.Y : away.X);
        var aside = MathF.Abs(primary is Direction.North or Direction.South ? away.X : away.Y);

        if (aside < along * CornerFraction)
            return [primary];

        var secondary = primary is Direction.North or Direction.South
            ? away.X >= 0f ? Direction.East : Direction.West
            : away.Y >= 0f ? Direction.North : Direction.South;

        return [primary, secondary];
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

    private Direction DirectionFrom(EntityUid user, EntityUid table)
    {
        return EmberProceduralTableVisuals.FlipDirection(
            _transform.GetWorldPosition(user), _transform.GetWorldPosition(table));
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
