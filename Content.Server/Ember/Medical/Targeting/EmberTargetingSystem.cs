using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Ember.Medical.Targeting;
using Content.Shared.Ember.Medical.Targeting.Events;
using Content.Shared.Body.Part;

namespace Content.Server.Ember.Medical.Targeting;
public sealed class EmberTargetingSystem : SharedEmberTargetingSystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EmberTargetChangeEvent>(OnTargetChange);
        SubscribeLocalEvent<EmberTargetingComponent, MobStateChangedEvent>(OnMobStateChange);
    }

    private void OnTargetChange(EmberTargetChangeEvent message, EntitySessionEventArgs args)
    {
        if (!TryComp<EmberTargetingComponent>(GetEntity(message.Uid), out var target))
            return;

        target.Target = message.BodyPart;
        Dirty(GetEntity(message.Uid), target);
    }

    private void OnMobStateChange(EntityUid uid, EmberTargetingComponent component, MobStateChangedEvent args)
    {
        // Revival is handled by the server, so we're keeping all of this here.
        var changed = false;

        if (args.NewMobState == MobState.Dead)
        {
            foreach (var part in GetValidParts())
            {
                component.BodyStatus[part] = EmberTargetIntegrity.Dead;
                changed = true;
            }
            // I love groin shitcode.
            component.BodyStatus[EmberTargetBodyPart.Groin] = EmberTargetIntegrity.Dead;
        }
        else if (args.OldMobState == MobState.Dead && (args.NewMobState == MobState.Alive || args.NewMobState == MobState.Critical))
        {
            component.BodyStatus = _bodySystem.GetBodyPartStatus(uid);
            changed = true;
        }

        if (changed)
        {
            Dirty(uid, component);
            RaiseNetworkEvent(new EmberTargetIntegrityChangeEvent(GetNetEntity(uid)), uid);
        }
    }
}
