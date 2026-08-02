using Content.Shared.DoAfter;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Skills;
using Content.Shared.Ember.Walls;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Structures;

/// <summary>
/// Bay lets you put things straight against a low wall rather than going through a build menu: a handful of rods
/// becomes a grille in it. The grille is made of whatever the rods were.
/// </summary>
public sealed class EmberAssembleIntoLowWallSystem : EntitySystem
{
    /// <summary>Bay's <c>place_grille</c> takes two rods and a second.</summary>
    private const int RodsPerGrille = 2;
    private const float AssembleSeconds = 1f;

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberMaterialStackComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EmberMaterialStackComponent, EmberAssembleGrilleDoAfterEvent>(OnAssembled);
    }

    private void OnAfterInteract(Entity<EmberMaterialStackComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!IsLowWall(target) || !TryGetGrille(ent.Comp, out var grille))
            return;

        args.Handled = true;

        if (HasGrilleOn(target))
        {
            _popup.PopupClient(Loc.GetString("ember-assemble-grille-occupied"), args.User, args.User);
            return;
        }

        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count < RodsPerGrille)
        {
            _popup.PopupClient(Loc.GetString("ember-assemble-grille-not-enough"), args.User, args.User);
            return;
        }

        // Skill decides how long it takes, through the same curve the construction menu uses. Nothing about
        // this is written into the recipe.
        var delay = AssembleSeconds * _skills.GetSkillDelayMultiplier(args.User, EmberConstructionSkill.Skill);

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            delay,
            new EmberAssembleGrilleDoAfterEvent(GetNetEntity(target)),
            ent,
            target,
            ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnAssembled(Entity<EmberMaterialStackComponent> ent, ref EmberAssembleGrilleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryGetEntity(args.LowWall, out var target) || !IsLowWall(target.Value) || HasGrilleOn(target.Value))
            return;

        if (!TryGetGrille(ent.Comp, out var grille) || !_stack.Use(ent, RodsPerGrille))
            return;

        // Spawned on the server only: the grille is a networked entity and the client has no business
        // predicting one into existence on a tile it does not own.
        if (_net.IsServer)
            Spawn(grille, _transform.GetMapCoordinates(target.Value));
    }

    private bool TryGetGrille(EmberMaterialStackComponent stack, out EntProtoId grille)
    {
        grille = default;

        if (!_prototype.TryIndex(stack.Material, out EmberMaterialPrototype? material) ||
            material.RodStack is not { } rods ||
            material.GrilleEntity is not { } result)
        {
            return false;
        }

        // Sheets of the same material carry the same component, so the stack type is what says these are rods.
        if (!TryComp<StackComponent>(stack.Owner, out var stackComponent) || stackComponent.StackTypeId != rods.Id)
            return false;

        grille = result;
        return true;
    }

    private bool IsLowWall(EntityUid uid)
    {
        return TryComp<EmberProceduralStructureComponent>(uid, out var structure) &&
               structure.Role == EmberProceduralStructureRole.WallFrame;
    }

    private bool HasGrilleOn(EntityUid lowWall)
    {
        var xform = Transform(lowWall);

        if (!xform.Anchored || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return false;

        var structures = GetEntityQuery<EmberProceduralStructureComponent>();
        var anchored = grid.GetAnchoredEntitiesEnumerator(grid.TileIndicesFor(xform.Coordinates));

        while (anchored.MoveNext(out var entity))
        {
            if (structures.TryGetComponent(entity.Value, out var other) &&
                other.Role == EmberProceduralStructureRole.Grille)
            {
                return true;
            }
        }

        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class EmberAssembleGrilleDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity LowWall;

    private EmberAssembleGrilleDoAfterEvent()
    {
    }

    public EmberAssembleGrilleDoAfterEvent(NetEntity lowWall)
    {
        LowWall = lowWall;
    }

    public override DoAfterEvent Clone() => this;
}
