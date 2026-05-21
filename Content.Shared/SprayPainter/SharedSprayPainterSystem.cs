using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Paint;
using Content.Shared.SprayPainter.Components;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.SprayPainter;

/// <summary>
/// System for painting airlocks using a spray painter.
/// Pipes are handled serverside since AtmosPipeColorSystem is server only.
/// </summary>
public abstract class SharedSprayPainterSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] private   readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] private   readonly SharedPopupSystem _popup = default!;

    public List<AirlockStyle> Styles { get; private set; } = new();
    public List<AirlockGroupPrototype> Groups { get; private set; } = new();

    [ValidatePrototypeId<AirlockDepartmentsPrototype>]
    private const string Departments = "Departments";

    public override void Initialize()
    {
        base.Initialize();

        CacheStyles();

        SubscribeLocalEvent<SprayPainterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SprayPainterComponent, SprayPainterDoorDoAfterEvent>(OnDoorDoAfter);
        SubscribeLocalEvent<SprayPainterComponent, SprayPainterWallDoAfterEvent>(OnWallDoAfter);
        Subs.BuiEvents<SprayPainterComponent>(SprayPainterUiKey.Key, subs =>
        {
            subs.Event<SprayPainterSpritePickedMessage>(OnSpritePicked);
            subs.Event<SprayPainterColorPickedMessage>(OnColorPicked);
            subs.Event<SprayPainterCustomColorPickedMessage>(OnCustomColorPicked);
            subs.Event<SprayPainterWallModePickedMessage>(OnWallModePicked);
            subs.Event<SprayPainterAirlockModePickedMessage>(OnAirlockModePicked);
        });

        SubscribeLocalEvent<PaintableAirlockComponent, InteractUsingEvent>(OnAirlockInteract);
        SubscribeLocalEvent<EmberProceduralWallComponent, InteractUsingEvent>(OnWallInteract);
        SubscribeLocalEvent<EmberProceduralStructureComponent, InteractUsingEvent>(OnStructureInteract);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnMapInit(Entity<SprayPainterComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ColorPalette.Count == 0)
            return;

        SetColor(ent, ent.Comp.ColorPalette.ContainsKey("white")
            ? "white"
            : ent.Comp.ColorPalette.First().Key);
    }

    private void OnDoorDoAfter(Entity<SprayPainterComponent> ent, ref SprayPainterDoorDoAfterEvent args)
    {
        ent.Comp.AirlockDoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not {} target)
            return;

        if (!TryComp<PaintableAirlockComponent>(target, out var airlock))
            return;

        if (args.Mode != SprayPainterAirlockMode.ApplyStyle)
        {
            if (!TryComp<EmberProceduralAirlockComponent>(target, out var emberPaintAirlock))
                return;

            SprayPainterAirlockPaint.Apply(emberPaintAirlock, args.Mode, args.Color);
            Dirty(target, emberPaintAirlock);

            Audio.PlayPredicted(ent.Comp.SpraySound, ent, args.Args.User);
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Args.User):user} painted {ToPrettyString(args.Args.Target.Value):target}");

            args.Handled = true;
            return;
        }

        airlock.Department = args.Department;
        Dirty(target, airlock);

        if (TryComp<EmberProceduralAirlockComponent>(target, out var emberAirlock) &&
            EmberAirlockPaintStyle.TryGetStyle(args.StyleName, out var emberStyle))
        {
            emberAirlock.Style = emberStyle;
            emberAirlock.DoorColor = null;
            emberAirlock.StripeColor = null;
            emberAirlock.WindowColor = null;
            Dirty(target, emberAirlock);
        }

        Audio.PlayPredicted(ent.Comp.SpraySound, ent, args.Args.User);
        Appearance.SetData(target, DoorVisuals.BaseRSI, args.Sprite);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Args.User):user} painted {ToPrettyString(args.Args.Target.Value):target}");

        args.Handled = true;
    }

    #region UI messages

    private void OnColorPicked(Entity<SprayPainterComponent> ent, ref SprayPainterColorPickedMessage args)
    {
        SetColor(ent, args.Key);
    }

    private void OnCustomColorPicked(Entity<SprayPainterComponent> ent, ref SprayPainterCustomColorPickedMessage args)
    {
        ent.Comp.PickedCustomColor = true;
        ent.Comp.CustomColor = args.Color.WithAlpha(1f);
        Dirty(ent, ent.Comp);
    }

    private void OnSpritePicked(Entity<SprayPainterComponent> ent, ref SprayPainterSpritePickedMessage args)
    {
        if (args.Index >= Styles.Count)
            return;

        ent.Comp.Index = args.Index;
        Dirty(ent, ent.Comp);
    }

    private void OnWallModePicked(Entity<SprayPainterComponent> ent, ref SprayPainterWallModePickedMessage args)
    {
        ent.Comp.WallMode = args.Mode;
        Dirty(ent, ent.Comp);
    }

    private void OnAirlockModePicked(Entity<SprayPainterComponent> ent, ref SprayPainterAirlockModePickedMessage args)
    {
        ent.Comp.AirlockMode = args.Mode;
        Dirty(ent, ent.Comp);
    }

    private void SetColor(Entity<SprayPainterComponent> ent, string? paletteKey)
    {
        if (paletteKey == null)
            return;

        if (!ent.Comp.ColorPalette.ContainsKey(paletteKey))
            return;

        if (!ent.Comp.PickedCustomColor && paletteKey == ent.Comp.PickedColor)
            return;

        ent.Comp.PickedCustomColor = false;
        ent.Comp.PickedColor = paletteKey;
        Dirty(ent, ent.Comp);
    }

    #endregion

    private void OnAirlockInteract(Entity<PaintableAirlockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SprayPainterComponent>(args.Used, out var painter) || painter.AirlockDoAfter != null)
            return;

        if (painter.AirlockMode != SprayPainterAirlockMode.ApplyStyle)
        {
            if (!HasComp<EmberProceduralAirlockComponent>(ent))
            {
                string msg = Loc.GetString("spray-painter-style-not-available");
                _popup.PopupClient(msg, args.User, args.User);
                return;
            }

            Color? color = null;
            if (SprayPainterAirlockPaint.RequiresColor(painter.AirlockMode))
            {
                if (!SprayPainterColorSelection.TryGetPickedColor(painter, out var pickedColor))
                {
                    _popup.PopupClient(Loc.GetString("pipe-painter-no-color-selected"), args.User, args.User);
                    return;
                }

                color = pickedColor;
            }

            var customDoAfter = new DoAfterArgs(
                EntityManager,
                args.User,
                painter.AirlockSprayTime,
                new SprayPainterDoorDoAfterEvent(string.Empty, null, string.Empty, painter.AirlockMode, color),
                args.Used,
                target: ent,
                used: args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (!DoAfter.TryStartDoAfter(customDoAfter, out var customId))
                return;

            painter.AirlockDoAfter = customId;
            args.Handled = true;
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} is painting {ToPrettyString(ent):target} at {Transform(ent).Coordinates:targetlocation}");
            return;
        }

        var group = Proto.Index<AirlockGroupPrototype>(ent.Comp.Group);

        var style = Styles[painter.Index];
        if (!group.StylePaths.TryGetValue(style.Name, out var sprite))
        {
            string msg = Loc.GetString("spray-painter-style-not-available");
            _popup.PopupClient(msg, args.User, args.User);
            return;
        }

        RemComp<PaintedComponent>(ent);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, painter.AirlockSprayTime, new SprayPainterDoorDoAfterEvent(sprite, style.Department, style.Name), args.Used, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (!DoAfter.TryStartDoAfter(doAfterEventArgs, out var id))
            return;

        // since we are now spraying an airlock prevent spraying more at the same time
        // pipes ignore this
        painter.AirlockDoAfter = id;
        args.Handled = true;

        // Log the attempt
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} is painting {ToPrettyString(ent):target} to '{style.Name}' at {Transform(ent).Coordinates:targetlocation}");
    }

    private void OnWallInteract(Entity<EmberProceduralWallComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SprayPainterComponent>(args.Used, out var painter) ||
            painter.AirlockDoAfter != null)
            return;

        Color? color = null;
        var pickedColor = default(Color);
        if (SprayPainterWallPaint.RequiresColor(painter.WallMode) &&
            !SprayPainterColorSelection.TryGetPickedColor(painter, out pickedColor))
        {
            _popup.PopupClient(Loc.GetString("pipe-painter-no-color-selected"), args.User, args.User);
            return;
        }

        if (SprayPainterWallPaint.RequiresColor(painter.WallMode))
            color = pickedColor;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, painter.AirlockSprayTime, new SprayPainterWallDoAfterEvent(painter.WallMode, color), args.Used, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!DoAfter.TryStartDoAfter(doAfterEventArgs, out var id))
            return;

        painter.AirlockDoAfter = id;
        args.Handled = true;
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} is painting {ToPrettyString(ent):target} at {Transform(ent).Coordinates:targetlocation}");
    }

    private void OnStructureInteract(Entity<EmberProceduralStructureComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SprayPainterComponent>(args.Used, out var painter) ||
            painter.AirlockDoAfter != null)
            return;

        if (!SprayPainterStructurePaint.CanApply(ent.Comp, painter.WallMode))
        {
            _popup.PopupClient(Loc.GetString("spray-painter-wall-mode-not-available"), args.User, args.User);
            return;
        }

        Color? color = null;
        if (SprayPainterWallPaint.RequiresColor(painter.WallMode))
        {
            if (!SprayPainterColorSelection.TryGetPickedColor(painter, out var pickedColor))
            {
                _popup.PopupClient(Loc.GetString("pipe-painter-no-color-selected"), args.User, args.User);
                return;
            }

            color = pickedColor;
        }

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, painter.AirlockSprayTime, new SprayPainterWallDoAfterEvent(painter.WallMode, color), args.Used, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!DoAfter.TryStartDoAfter(doAfterEventArgs, out var id))
            return;

        painter.AirlockDoAfter = id;
        args.Handled = true;
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} is painting {ToPrettyString(ent):target} at {Transform(ent).Coordinates:targetlocation}");
    }

    private void OnWallDoAfter(Entity<SprayPainterComponent> ent, ref SprayPainterWallDoAfterEvent args)
    {
        ent.Comp.AirlockDoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not { } target)
            return;

        if (TryComp<EmberProceduralWallComponent>(target, out var wall))
        {
            SprayPainterWallPaint.Apply(wall, args.Mode, args.Color);
            Dirty(target, wall);
        }
        else if (TryComp<EmberProceduralStructureComponent>(target, out var structure) &&
                 SprayPainterStructurePaint.CanApply(structure, args.Mode))
        {
            SprayPainterStructurePaint.Apply(structure, args.Mode, args.Color);
            Dirty(target, structure);
        }
        else
        {
            return;
        }

        Audio.PlayPredicted(ent.Comp.SpraySound, ent, args.Args.User);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.Args.User):user} painted {ToPrettyString(args.Args.Target.Value):target}");

        args.Handled = true;
    }

    #region Style caching

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<AirlockGroupPrototype>() && !args.WasModified<AirlockDepartmentsPrototype>())
            return;

        Styles.Clear();
        Groups.Clear();
        CacheStyles();

        // style index might be invalid now so check them all
        var max = Styles.Count - 1;
        var query = AllEntityQuery<SprayPainterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Index > max)
            {
                comp.Index = max;
                Dirty(uid, comp);
            }
        }
    }

    protected virtual void CacheStyles()
    {
        // collect every style's name
        var names = new SortedSet<string>();
        foreach (var group in Proto.EnumeratePrototypes<AirlockGroupPrototype>())
        {
            Groups.Add(group);
            foreach (var style in group.StylePaths.Keys)
            {
                names.Add(style);
            }
        }

        // get their department ids too for the final style list
        var departments = Proto.Index<AirlockDepartmentsPrototype>(Departments);
        Styles.Capacity = names.Count;
        foreach (var name in names)
        {
            departments.Departments.TryGetValue(name, out var department);
            Styles.Add(new AirlockStyle(name, department));
        }
    }

    #endregion
}

public record struct AirlockStyle(string Name, string? Department);
