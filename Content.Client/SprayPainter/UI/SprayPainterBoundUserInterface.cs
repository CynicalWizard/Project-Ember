using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.SprayPainter.UI;

public sealed class SprayPainterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SprayPainterWindow? _window;

    public SprayPainterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SprayPainterWindow>();

        _window.OnSpritePicked = OnSpritePicked;
        _window.OnColorPicked = OnColorPicked;
        _window.OnCustomColorPicked = OnCustomColorPicked;
        _window.OnWallModePicked = OnWallModePicked;
        _window.OnAirlockModePicked = OnAirlockModePicked;
        _window.OnClosetPicked = OnClosetPicked;

        if (EntMan.TryGetComponent(Owner, out SprayPainterComponent? comp))
        {
            _window.Populate(
                EntMan.System<SprayPainterSystem>().Entries,
                EntMan.System<SprayPainterSystem>().ClosetEntries,
                comp.Index,
                comp.ClosetIndex,
                comp.WallMode,
                comp.AirlockMode,
                comp.PickedColor,
                comp.PickedCustomColor,
                comp.CustomColor,
                comp.ColorPalette);
        }
    }

    private void OnSpritePicked(ItemList.ItemListSelectedEventArgs args)
    {
        SendMessage(new SprayPainterSpritePickedMessage(args.ItemIndex));
    }

    private void OnColorPicked(ItemList.ItemListSelectedEventArgs args)
    {
        var key = _window?.IndexToColorKey(args.ItemIndex);
        SendMessage(new SprayPainterColorPickedMessage(key));
    }

    private void OnCustomColorPicked(Color color)
    {
        SendMessage(new SprayPainterCustomColorPickedMessage(color));
    }

    private void OnWallModePicked(ItemList.ItemListSelectedEventArgs args)
    {
        if (_window == null)
            return;

        SendMessage(new SprayPainterWallModePickedMessage(_window.IndexToWallMode(args.ItemIndex)));
    }

    private void OnClosetPicked(ItemList.ItemListSelectedEventArgs args)
    {
        SendMessage(new SprayPainterClosetPickedMessage(args.ItemIndex));
    }

    private void OnAirlockModePicked(ItemList.ItemListSelectedEventArgs args)
    {
        if (_window == null)
            return;

        SendMessage(new SprayPainterAirlockModePickedMessage(_window.IndexToAirlockMode(args.ItemIndex)));
    }
}
