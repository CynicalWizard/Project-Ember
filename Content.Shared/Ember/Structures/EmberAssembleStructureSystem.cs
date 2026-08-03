using System.Diagnostics.CodeAnalysis;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.DoAfter;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Structures;

/// <summary>
/// Bay lets you build against a low wall by hand rather than through the build menu: rods become a grille in it,
/// and glass held against the low wall or its grille becomes a window.
/// </summary>
/// <remarks>
/// Neither half knows a material by name. The grille comes from the material prototype, and the window comes from
/// the ordinary construction recipe, so a window you place by hand costs exactly what the build menu charges and a
/// new kind of glass needs no code at all.
/// </remarks>
public sealed class EmberAssembleStructureSystem : EntitySystem
{
    /// <summary>Bay's <c>place_grille</c> takes two rods and a second.</summary>
    private const int RodsPerGrille = 2;
    private const float AssembleSeconds = 1f;

    /// <summary>Bay's <c>place_window</c> spends two seconds regardless of the glass.</summary>
    private const float PlaceWindowSeconds = 2f;

    private static readonly ProtoId<ConstructionGraphPrototype> WindowGraph = "Window";

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

        // Glass sheets lay floor tiles on the same event, and that runs off the clicked tile rather than the
        // clicked entity, so it would happily re-floor the ground under a low wall you were glazing.
        SubscribeLocalEvent<EmberMaterialStackComponent, AfterInteractEvent>(OnAfterInteract,
            before: new[] { typeof(FloorTileSystem) });
        SubscribeLocalEvent<EmberMaterialStackComponent, EmberAssembleGrilleDoAfterEvent>(OnAssembled);
        SubscribeLocalEvent<EmberMaterialStackComponent, EmberPlaceWindowDoAfterEvent>(OnWindowPlaced);
    }

    private void OnAfterInteract(Entity<EmberMaterialStackComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartGrille(ent, target, args.User))
        {
            args.Handled = true;
            return;
        }

        if (TryStartWindow(ent, target, args.User))
            args.Handled = true;
    }

    #region Grille

    private bool TryStartGrille(Entity<EmberMaterialStackComponent> ent, EntityUid target, EntityUid user)
    {
        if (!IsLowWall(target) || !TryGetGrille(ent.Comp, out var grille))
            return false;

        if (HasGrilleOn(target))
        {
            _popup.PopupClient(Loc.GetString("ember-assemble-grille-occupied"), user, user);
            return true;
        }

        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count < RodsPerGrille)
        {
            _popup.PopupClient(Loc.GetString("ember-assemble-grille-not-enough"), user, user);
            return true;
        }

        StartDoAfter(ent, target, user, AssembleSeconds, new EmberAssembleGrilleDoAfterEvent(GetNetEntity(target)));
        return true;
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

    #endregion

    #region Window

    private bool TryStartWindow(Entity<EmberMaterialStackComponent> ent, EntityUid target, EntityUid user)
    {
        if (!IsLowWall(target) && !IsGrille(target))
            return false;

        if (!TryGetWindowRecipe(ent, out var node, out var sheets))
            return false;

        if (!TileTakesAWindow(target, user))
        {
            _popup.PopupClient(Loc.GetString("ember-place-window-occupied"), user, user);
            return true;
        }

        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count < sheets)
        {
            _popup.PopupClient(Loc.GetString("ember-place-window-not-enough", ("amount", sheets)), user, user);
            return true;
        }

        StartDoAfter(ent, target, user, PlaceWindowSeconds,
            new EmberPlaceWindowDoAfterEvent(GetNetEntity(target), node, sheets));
        return true;
    }

    private void OnWindowPlaced(Entity<EmberMaterialStackComponent> ent, ref EmberPlaceWindowDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryGetEntity(args.Frame, out var target) || !TileTakesAWindow(target.Value, args.User))
            return;

        // Re-read the recipe rather than trusting the numbers the do-after carried, in case the stack changed
        // hands or was split while the window was going up.
        if (!TryGetWindowRecipe(ent, out var node, out var sheets) ||
            node != args.Node ||
            sheets != args.Sheets)
        {
            return;
        }

        // Which entity a node builds is server-only data, which is why the client only ever gets as far as
        // agreeing that the recipe exists. Resolve it before spending the glass, so a recipe that turns out to
        // build nothing costs the player nothing either.
        string? window = null;

        if (_net.IsServer)
        {
            if (!_prototype.TryIndex(WindowGraph, out ConstructionGraphPrototype? graph) ||
                !graph.Nodes.TryGetValue(node, out var graphNode) ||
                graphNode.Entity.GetId(null, args.User, new GraphNodeEntityArgs(EntityManager)) is not { } id)
            {
                return;
            }

            window = id;
        }

        if (!_stack.Use(ent, sheets))
            return;

        if (window != null)
            Spawn(window, _transform.GetMapCoordinates(target.Value));
    }

    /// <summary>
    /// Finds the build-menu recipe that turns this stack into a full-tile window, and how many sheets it wants.
    /// </summary>
    /// <remarks>
    /// Only recipes made of glass and nothing else can be held up against a frame; the shuttle window also wants
    /// plasteel and stays in the build menu, which is where Bay leaves it too.
    /// </remarks>
    private bool TryGetWindowRecipe(EntityUid uid, [NotNullWhen(true)] out string? node, out int sheets)
    {
        node = null;
        sheets = 0;

        if (!TryComp<StackComponent>(uid, out var stack) ||
            !_prototype.TryIndex(WindowGraph, out ConstructionGraphPrototype? graph) ||
            graph.Start is not { } start ||
            !graph.Nodes.TryGetValue(start, out var startNode))
        {
            return false;
        }

        foreach (var edge in startNode.Edges)
        {
            if (edge.Steps.Count != 1 ||
                edge.Steps[0] is not MaterialConstructionGraphStep material ||
                material.MaterialPrototypeId != stack.StackTypeId)
            {
                continue;
            }

            // Reinforced glass builds both a plain and a tinted window. Nothing here can ask which one you
            // wanted, so hand placement gives you the cheaper one and the build menu keeps the choice.
            if (node != null && material.Amount >= sheets)
                continue;

            node = edge.Target;
            sheets = material.Amount;
        }

        return node != null;
    }

    /// <summary>
    /// Uses the build menu's own rule, so a tile that refuses a window there refuses one here.
    /// </summary>
    private bool TileTakesAWindow(EntityUid frame, EntityUid user)
    {
        return new NoWindowsInTile().Condition(user, Transform(frame).Coordinates, Direction.South);
    }

    #endregion

    private void StartDoAfter(EntityUid used, EntityUid target, EntityUid user, float seconds, DoAfterEvent ev)
    {
        // Skill decides how long it takes, through the same curve the construction menu uses. Nothing about
        // this is written into the recipe.
        var delay = seconds * _skills.GetSkillDelayMultiplier(user, EmberConstructionSkill.Skill);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, ev, used, target, used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private bool IsLowWall(EntityUid uid)
    {
        return TryComp<EmberProceduralStructureComponent>(uid, out var structure) &&
               structure.Role == EmberProceduralStructureRole.WallFrame;
    }

    private bool IsGrille(EntityUid uid)
    {
        return TryComp<EmberProceduralStructureComponent>(uid, out var structure) &&
               structure.Role == EmberProceduralStructureRole.Grille;
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

[Serializable, NetSerializable]
public sealed partial class EmberPlaceWindowDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Frame;

    [DataField]
    public string Node = string.Empty;

    [DataField]
    public int Sheets;

    private EmberPlaceWindowDoAfterEvent()
    {
    }

    public EmberPlaceWindowDoAfterEvent(NetEntity frame, string node, int sheets)
    {
        Frame = frame;
        Node = node;
        Sheets = sheets;
    }

    public override DoAfterEvent Clone() => this;
}
