using Content.Client.Gameplay;
using Content.Client.Ember.Medical.PartStatus.Widgets;
using Content.Shared.Ember.Medical.Targeting;
using Content.Client.Ember.Medical.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Utility;
using Robust.Client.Graphics;


namespace Content.Client.Ember.Medical.PartStatus;

public sealed class EmberPartStatusUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<EmberTargetingSystem>
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private SpriteSystem _spriteSystem = default!;
    private EmberTargetingComponent? _targetingComponent;
    private EmberPartStatusControl? EmberPartStatusControl => UIManager.GetActiveUIWidgetOrNull<EmberPartStatusControl>();

    public void OnSystemLoaded(EmberTargetingSystem system)
    {
        system.PartStatusStartup += AddPartStatusControl;
        system.PartStatusShutdown += RemovePartStatusControl;
        system.PartStatusUpdate += UpdatePartStatusControl;
    }

    public void OnSystemUnloaded(EmberTargetingSystem system)
    {
        system.PartStatusStartup -= AddPartStatusControl;
        system.PartStatusShutdown -= RemovePartStatusControl;
        system.PartStatusUpdate -= UpdatePartStatusControl;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (EmberPartStatusControl != null)
        {
            EmberPartStatusControl.SetVisible(_targetingComponent != null);

            if (_targetingComponent != null)
                EmberPartStatusControl.SetTextures(_targetingComponent.BodyStatus);
        }
    }

    public void AddPartStatusControl(EmberTargetingComponent component)
    {
        _targetingComponent = component;

        if (EmberPartStatusControl != null)
        {
            EmberPartStatusControl.SetVisible(_targetingComponent != null);

            if (_targetingComponent != null)
                EmberPartStatusControl.SetTextures(_targetingComponent.BodyStatus);
        }

    }

    public void RemovePartStatusControl()
    {
        if (EmberPartStatusControl != null)
            EmberPartStatusControl.SetVisible(false);

        _targetingComponent = null;
    }

    public void UpdatePartStatusControl(EmberTargetingComponent component)
    {
        if (EmberPartStatusControl != null && _targetingComponent != null)
            EmberPartStatusControl.SetTextures(_targetingComponent.BodyStatus);
    }

    public Texture GetTexture(SpriteSpecifier specifier)
    {
        if (_spriteSystem == null)
            _spriteSystem = _entManager.System<SpriteSystem>();

        return _spriteSystem.Frame0(specifier);
    }
}
