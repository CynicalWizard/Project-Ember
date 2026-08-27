using Content.Server.Atmos.Rotting;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Ember.Medical.Surgery;
using Content.Shared.Ember.Medical.Surgery.Conditions;
using Content.Shared.Ember.Medical.Surgery.Effects.Step;
using Content.Shared.Ember.Medical.Surgery.Steps;
using Content.Shared.Ember.Medical.Surgery.Steps.Parts;
using Content.Shared.Ember.Medical.Surgery.Tools;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Verbs;

namespace Content.Server.Ember.Medical.Surgery;

public sealed class EmberSurgerySystem : SharedEmberSurgerySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly RottingSystem _rot = default!;
    [Dependency] private readonly BlindableSystem _blindableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberSurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<EmberSurgeryTargetComponent, EmberSurgeryStepDamageEvent>(OnSurgeryStepDamage);
        // You might be wondering "why aren't we using StepEvent for these two?" reason being that StepEvent fires off regardless of success on the previous functions
        // so this would heal entities even if you had a used or incorrect organ.
        SubscribeLocalEvent<EmberSurgerySpecialDamageChangeEffectComponent, EmberSurgeryStepDamageChangeEvent>(OnSurgerySpecialDamageChange);
        SubscribeLocalEvent<EmberSurgeryDamageChangeEffectComponent, EmberSurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<EmberSurgeryStepEmoteEffectComponent, EmberSurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<EmberSurgeryStepSpawnEffectComponent, EmberSurgeryStepEvent>(OnStepSpawnComplete);
    }

    protected override void RefreshUI(EntityUid body)
    {
        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        foreach (var surgery in AllSurgeries)
        {
            if (GetSingleton(surgery) is not { } surgeryEnt)
                continue;

            foreach (var part in _body.GetBodyChildren(body))
            {
                var ev = new EmberSurgeryValidEvent(body, part.Id);
                RaiseLocalEvent(surgeryEnt, ref ev);

                if (ev.Cancelled)
                    continue;

                surgeries.GetOrNew(GetNetEntity(part.Id)).Add(surgery);
            }

        }
        _ui.SetUiState(body, EmberSurgeryUIKey.Key, new EmberSurgeryBuiState(surgeries));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, EmberSurgeryUIKey.Key, new EmberSurgeryBuiRefreshMessage());
    }
    private void SetDamage(EntityUid body,
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return;

        _damageable.TryChangeDamage(body,
            damage,
            true,
            origin: user,
            canSever: false,
            partMultiplier: partMultiplier,
            targetPart: _body.GetTargetBodyPart(partComp));
    }

    private void AttemptStartSurgery(Entity<EmberSurgeryToolComponent> ent, EntityUid user, EntityUid target)
    {
        if (!IsLyingDown(target, user))
            return;

        if (user == target && !_config.GetCVar(CCVars.CanOperateOnSelf))
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-self-surgery"), user, user);
            return;
        }

        _ui.OpenUi(target, EmberSurgeryUIKey.Key, user);
        RefreshUI(target);
    }

    private void OnUtilityVerb(Entity<EmberSurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract
            || !args.CanAccess
            || !HasComp<EmberSurgeryTargetComponent>(args.Target))
            return;

        var user = args.User;
        var target = args.Target;

        var verb = new UtilityVerb()
        {
            Act = () => AttemptStartSurgery(ent, user, target),
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi/"), "scalpel"),
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            DoContactInteraction = true
        };

        args.Verbs.Add(verb);
    }

    private void OnSurgeryStepDamage(Entity<EmberSurgeryTargetComponent> ent, ref EmberSurgeryStepDamageEvent args) =>
        SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part);

    private void OnSurgeryDamageChange(Entity<EmberSurgeryDamageChangeEffectComponent> ent, ref EmberSurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (HasComp<ForcedSleepingComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part);
    }

    private void OnSurgerySpecialDamageChange(Entity<EmberSurgerySpecialDamageChangeEffectComponent> ent, ref EmberSurgeryStepDamageChangeEvent args)
    {
        // Im killing this shit soon too, inshallah.
        if (ent.Comp.DamageType == "Rot")
            _rot.ReduceAccumulator(args.Body, TimeSpan.FromSeconds(2147483648)); // BEHOLD, SHITCODE THAT I JUST COPY PASTED. I'll redo it at some point, pinky swear :)
        /*else if (ent.Comp.DamageType == "Eye"
            && TryComp(ent, out BlindableComponent? blindComp)
            && blindComp.EyeDamage > 0)
            _blindableSystem.AdjustEyeDamage((args.Body, blindComp), -blindComp!.EyeDamage);*/
    }

    private void OnStepScreamComplete(Entity<EmberSurgeryStepEmoteEffectComponent> ent, ref EmberSurgeryStepEvent args)
    {
        if (HasComp<ForcedSleepingComponent>(args.Body) || HasComp<EmberNoScreamComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
    private void OnStepSpawnComplete(Entity<EmberSurgeryStepSpawnEffectComponent> ent, ref EmberSurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);
}
