using Content.Client.Gameplay;
using Content.Client.Ember.Medical.Targeting.Widgets;
using Content.Shared.Ember.Medical.Targeting;
using Content.Client.Ember.Medical.Targeting;
using Content.Shared.Ember.Medical.Targeting.Events;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.Player;

namespace Content.Client.Ember.Medical.Targeting;

public sealed class EmberTargetingUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<EmberTargetingSystem>
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private EmberTargetingComponent? _targetingComponent;
    private EmberTargetingControl? EmberTargetingControl => UIManager.GetActiveUIWidgetOrNull<EmberTargetingControl>();

    public void OnSystemLoaded(EmberTargetingSystem system)
    {
        system.TargetingStartup += AddTargetingControl;
        system.TargetingShutdown += RemoveTargetingControl;
        system.TargetChange += CycleTarget;
    }

    public void OnSystemUnloaded(EmberTargetingSystem system)
    {
        system.TargetingStartup -= AddTargetingControl;
        system.TargetingShutdown -= RemoveTargetingControl;
        system.TargetChange -= CycleTarget;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (EmberTargetingControl == null)
            return;

        EmberTargetingControl.SetTargetDollVisible(_targetingComponent != null);

        if (_targetingComponent != null)
            EmberTargetingControl.SetBodyPartsVisible(_targetingComponent.Target);
    }

    public void AddTargetingControl(EmberTargetingComponent component)
    {
        _targetingComponent = component;

        if (EmberTargetingControl != null)
        {
            EmberTargetingControl.SetTargetDollVisible(_targetingComponent != null);

            if (_targetingComponent != null)
                EmberTargetingControl.SetBodyPartsVisible(_targetingComponent.Target);
        }

    }

    public void RemoveTargetingControl()
    {
        if (EmberTargetingControl != null)
            EmberTargetingControl.SetTargetDollVisible(false);

        _targetingComponent = null;
    }

    public void CycleTarget(EmberTargetBodyPart bodyPart)
    {
        if (_playerManager.LocalEntity is not { } user
            || _entManager.GetComponent<EmberTargetingComponent>(user) is not { } targetingComponent
            || EmberTargetingControl == null)
            return;

        var player = _entManager.GetNetEntity(user);
        if (bodyPart != targetingComponent.Target)
        {
            var msg = new EmberTargetChangeEvent(player, bodyPart);
            _net.SendSystemNetworkMessage(msg);
            EmberTargetingControl?.SetBodyPartsVisible(bodyPart);
        }
    }
}
