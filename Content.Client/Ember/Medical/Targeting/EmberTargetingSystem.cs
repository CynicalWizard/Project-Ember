using Content.Shared.Input;
using Content.Shared.Ember.Medical.Targeting;
using Content.Shared.Ember.Medical.Targeting.Events;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client.Ember.Medical.Targeting;
public sealed class EmberTargetingSystem : SharedEmberTargetingSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public event Action<EmberTargetingComponent>? TargetingStartup;
    public event Action? TargetingShutdown;
    public event Action<EmberTargetBodyPart>? TargetChange;
    public event Action<EmberTargetingComponent>? PartStatusStartup;
    public event Action<EmberTargetingComponent>? PartStatusUpdate;
    public event Action? PartStatusShutdown;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmberTargetingComponent, LocalPlayerAttachedEvent>(HandlePlayerAttached);
        SubscribeLocalEvent<EmberTargetingComponent, LocalPlayerDetachedEvent>(HandlePlayerDetached);
        SubscribeLocalEvent<EmberTargetingComponent, ComponentStartup>(OnTargetingStartup);
        SubscribeLocalEvent<EmberTargetingComponent, ComponentShutdown>(OnTargetingShutdown);
        SubscribeNetworkEvent<EmberTargetIntegrityChangeEvent>(OnTargetIntegrityChange);

        CommandBinds.Builder
        .Bind(ContentKeyFunctions.TargetHead,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.Head)))
        .Bind(ContentKeyFunctions.TargetTorso,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.Torso)))
        .Bind(ContentKeyFunctions.TargetLeftArm,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.LeftArm)))
        .Bind(ContentKeyFunctions.TargetLeftHand,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.LeftHand)))
        .Bind(ContentKeyFunctions.TargetRightArm,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.RightArm)))
        .Bind(ContentKeyFunctions.TargetRightHand,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.RightHand)))
        .Bind(ContentKeyFunctions.TargetLeftLeg,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.LeftLeg)))
        .Bind(ContentKeyFunctions.TargetLeftFoot,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.LeftFoot)))
        .Bind(ContentKeyFunctions.TargetRightLeg,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.RightLeg)))
        .Bind(ContentKeyFunctions.TargetRightFoot,
            InputCmdHandler.FromDelegate((session) => HandleTargetChange(session, EmberTargetBodyPart.RightFoot)))
        .Register<SharedEmberTargetingSystem>();
    }

    private void HandlePlayerAttached(EntityUid uid, EmberTargetingComponent component, LocalPlayerAttachedEvent args)
    {
        TargetingStartup?.Invoke(component);
        PartStatusStartup?.Invoke(component);
    }

    private void HandlePlayerDetached(EntityUid uid, EmberTargetingComponent component, LocalPlayerDetachedEvent args)
    {
        TargetingShutdown?.Invoke();
        PartStatusShutdown?.Invoke();
    }

    private void OnTargetingStartup(EntityUid uid, EmberTargetingComponent component, ComponentStartup args)
    {
        if (_playerManager.LocalEntity != uid)
            return;

        TargetingStartup?.Invoke(component);
        PartStatusStartup?.Invoke(component);
    }

    private void OnTargetingShutdown(EntityUid uid, EmberTargetingComponent component, ComponentShutdown args)
    {
        if (_playerManager.LocalEntity != uid)
            return;

        TargetingShutdown?.Invoke();
        PartStatusShutdown?.Invoke();
    }

    private void OnTargetIntegrityChange(EmberTargetIntegrityChangeEvent args)
    {
        if (!TryGetEntity(args.Uid, out var uid)
            || !_playerManager.LocalEntity.Equals(uid)
            || !TryComp(uid, out EmberTargetingComponent? component)
            || !args.RefreshUi)
            return;

        PartStatusUpdate?.Invoke(component);
    }

    private void HandleTargetChange(ICommonSession? session, EmberTargetBodyPart target)
    {
        if (session == null
            || session.AttachedEntity is not { } uid
            || !TryComp<EmberTargetingComponent>(uid, out var targeting))
            return;

        TargetChange?.Invoke(target);
    }
}
