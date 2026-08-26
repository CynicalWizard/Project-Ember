using Content.Shared._Goobstation.Exorcism;
using Content.Shared._Goobstation.Religion;
using Content.Server.Bible.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Goobstation.Bible;

public sealed partial class GoobBibleSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public void TryDoSmite(EntityUid uid, BibleComponent component, AfterInteractUsingEvent args, UseDelayComponent useDelay)
    {
        if (args.Target is not { } target || !HasComp<WeakToHolyComponent>(args.Target) || !HasComp<BibleUserComponent>(args.User))
            return;

        // Ember: the devil-specific multiplier and exorcism branch went with the Devil
        // antagonist. The ordinary smite against anything WeakToHoly is unchanged.
        var multiplier = 1f;

        if (!_mobStateSystem.IsIncapacitated(target))
        {
            var popup = Loc.GetString("weaktoholy-component-bible-sizzle", ("target", target), ("item", args.Used));
            _popupSystem.PopupEntity(popup, target, PopupType.LargeCaution);
            _audio.PlayPvs(component.SizzleSoundPath, args.Target.Value);

            _damageableSystem.TryChangeDamage(target, component.SmiteDamage * multiplier, true, origin: uid);
            _stun.TryParalyze(target, component.SmiteStunDuration * multiplier, false);
            _delay.TryResetDelay((args.Used, useDelay));
        }
    }
}
