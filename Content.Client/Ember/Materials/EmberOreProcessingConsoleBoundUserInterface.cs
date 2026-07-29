using Content.Shared.Ember.Materials;
using Robust.Client.UserInterface;

namespace Content.Client.Ember.Materials;

public sealed class EmberOreProcessingConsoleBoundUserInterface : BoundUserInterface
{
    private EmberOreProcessingConsoleWindow? _window;

    public EmberOreProcessingConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<EmberOreProcessingConsoleWindow>();
        _window.OnRelink += () => SendMessage(new EmberOreConsoleRelinkMessage());
        _window.OnInputChanged += (kind, direction) => SendMessage(new EmberOreConsoleSetDirectionMessage(kind, true, direction));
        _window.OnOutputChanged += (kind, direction) => SendMessage(new EmberOreConsoleSetDirectionMessage(kind, false, direction));
        _window.OnToggleProcessor += () => SendMessage(new EmberOreConsoleToggleProcessorMessage());
        _window.OnPreset += preset => SendMessage(new EmberOreConsolePresetModesMessage(preset));
        _window.OnOreModeChanged += (material, mode) => SendMessage(new EmberOreConsoleSetProcessorModeMessage(material, mode));
        _window.OnStackAmountChanged += amount => SendMessage(new EmberOreConsoleSetStackAmountMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not EmberOreProcessingConsoleState consoleState)
            return;

        _window.UpdateState(consoleState);
    }
}
