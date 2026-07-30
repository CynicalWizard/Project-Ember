using Content.Server.Administration.Managers;
using Content.Server.Sandbox;
using Content.Shared.Administration;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.SprayPainter;
using Robust.Shared.Console;

namespace Content.Server.Ember.Mapping;

[AnyCommand]
public sealed class EmberPaintCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    public string Command => "emberpaint";
    public string Description => "Paints Ember procedural walls, low walls, and airlocks for mapping.";
    public string Help => "emberpaint <netEntity> <mode> <#RRGGBB>. Modes: wall, wallclear, stripe, stripeclear, airlockdoor, airlockdoorclear, airlockstripe, airlockstripeclear, airlockwindow, airlockwindowclear.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sandboxManager = _entity.System<SandboxSystem>();
        if (shell.IsClient && (!sandboxManager.IsSandboxEnabled && !_adminManager.HasAdminFlag(shell.Player!, AdminFlags.Mapping)))
        {
            shell.WriteError("You are not currently able to use mapping commands.");
            return;
        }

        if (args.Length != 3)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var targetId) ||
            !_entity.TryGetEntity(new NetEntity(targetId), out var uid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        var color = Color.TryFromHex(args[2]);
        if (!color.HasValue)
        {
            shell.WriteError(Loc.GetString("shell-invalid-color-hex"));
            return;
        }

        if (!TryPaint(uid.Value, args[1].ToLowerInvariant(), color.Value.WithAlpha(1f)))
            shell.WriteError("Target cannot use that Ember paint mode.");
    }

    private bool TryPaint(EntityUid uid, string mode, Color color)
    {
        switch (mode)
        {
            case "wall":
                if (_entity.TryGetComponent(uid, out EmberProceduralWallComponent? wall))
                {
                    SprayPainterWallPaint.Apply(wall, SprayPainterWallMode.PaintWall, color);
                    _entity.Dirty(uid, wall);
                    return true;
                }

                if (_entity.TryGetComponent(uid, out EmberProceduralStructureComponent? frame) &&
                    SprayPainterStructurePaint.CanApply(frame, SprayPainterWallMode.PaintWall))
                {
                    SprayPainterStructurePaint.Apply(frame, SprayPainterWallMode.PaintWall, color);
                    _entity.Dirty(uid, frame);
                    return true;
                }

                return false;

            case "wallclear":
                if (_entity.TryGetComponent(uid, out EmberProceduralWallComponent? clearWall))
                {
                    SprayPainterWallPaint.Apply(clearWall, SprayPainterWallMode.ClearWallPaint, null);
                    _entity.Dirty(uid, clearWall);
                    return true;
                }

                if (_entity.TryGetComponent(uid, out EmberProceduralStructureComponent? clearFrame) &&
                    SprayPainterStructurePaint.CanApply(clearFrame, SprayPainterWallMode.ClearWallPaint))
                {
                    SprayPainterStructurePaint.Apply(clearFrame, SprayPainterWallMode.ClearWallPaint, null);
                    _entity.Dirty(uid, clearFrame);
                    return true;
                }

                return false;

            case "stripe":
                return TryPaintWall(uid, SprayPainterWallMode.PaintStripe, color);
            case "stripeclear":
                return TryPaintWall(uid, SprayPainterWallMode.ClearStripe, null);
            case "airlockdoor":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.PaintDoor, color);
            case "airlockdoorclear":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.ClearDoor, null);
            case "airlockstripe":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.PaintStripe, color);
            case "airlockstripeclear":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.ClearStripe, null);
            case "airlockwindow":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.PaintWindow, color);
            case "airlockwindowclear":
                return TryPaintAirlock(uid, SprayPainterAirlockMode.ClearWindow, null);
        }

        return false;
    }

    private bool TryPaintWall(EntityUid uid, SprayPainterWallMode mode, Color? color)
    {
        if (!_entity.TryGetComponent(uid, out EmberProceduralWallComponent? wall))
            return false;

        SprayPainterWallPaint.Apply(wall, mode, color);
        _entity.Dirty(uid, wall);
        return true;
    }

    private bool TryPaintAirlock(EntityUid uid, SprayPainterAirlockMode mode, Color? color)
    {
        if (!_entity.TryGetComponent(uid, out EmberProceduralAirlockComponent? airlock))
            return false;

        SprayPainterAirlockPaint.Apply(airlock, mode, color);
        _entity.Dirty(uid, airlock);
        return true;
    }
}
