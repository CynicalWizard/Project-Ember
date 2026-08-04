using Content.Client.Gameplay;
using Content.Client.Sandbox;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Ember.Tiles;

/// <summary>
/// Opens <see cref="EmberTileSpawnWindow"/> in place of the engine's tile list.
/// </summary>
public sealed class EmberTileSpawnUIController : UIController, IOnStateExited<GameplayState>,
    IOnSystemChanged<SandboxSystem>
{
    [UISystemDependency] private readonly SandboxSystem _sandbox = default!;

    private EmberTileSpawnWindow? _window;

    public void ToggleWindow()
    {
        if (_window is { Disposed: false } window)
        {
            if (window.IsOpen)
                window.Close();
            else if (_sandbox.SandboxAllowed)
                window.Open();

            return;
        }

        if (!_sandbox.SandboxAllowed)
            return;

        _window = UIManager.CreateWindow<EmberTileSpawnWindow>();
        _window.OpenCentered();
    }

    public void CloseWindow()
    {
        _window?.Close();
    }

    public void OnStateExited(GameplayState state)
    {
        _window?.Dispose();
        _window = null;
    }

    public void OnSystemLoaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled += CloseWindow;
    }

    public void OnSystemUnloaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled -= CloseWindow;
    }
}
