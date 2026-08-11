using Content.Shared.Ember.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Clothing;

/// <summary>
/// Puts the "remove accessory" verb on clothing that has accessories attached, and opens the
/// radial menu when there is more than one thing to choose between.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's removetie_verb() (code/modules/clothing/clothing_accessories.dm),
/// which also skips the menu entirely when only a single accessory is attached. The verb is
/// client-exclusive so the menu can be opened without giving every uniform a UserInterface
/// component; the actual removal goes back to the shared system as a predicted event.
/// </remarks>
public sealed class EmberAccessoryMenuSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private EmberAccessoryRadialMenu? _menu;

    public override void Initialize()
    {
        base.Initialize();

        // EmberAccessorySystem owns the verb events on this component, so the menu hangs off the
        // hook it re-raises instead of subscribing to them a second time.
        SubscribeLocalEvent<EmberAccessoryHolderComponent, EmberAccessoryGetVerbsEvent>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<EmberAccessoryHolderComponent> holder, ref EmberAccessoryGetVerbsEvent ev)
    {
        var args = ev.Args;

        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (holder.Comp.Container is not { Count: > 0 } container)
            return;

        var removable = new List<EntityUid>();
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if ((comp.Flags & EmberAccessoryFlags.Removable) == 0)
                continue;

            removable.Add(accessory);
        }

        if (removable.Count == 0)
            return;

        var target = holder.Owner;

        args.Verbs.Add(new EquipmentVerb
        {
            Text = Loc.GetString("ember-accessory-remove-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            ClientExclusive = true,
            Act = () =>
            {
                if (removable.Count == 1)
                    RaisePredictiveEvent(new EmberAccessoryDetachRequestEvent(GetNetEntity(removable[0])));
                else
                    OpenMenu(target);
            },
        });
    }

    private void OpenMenu(EntityUid holder)
    {
        CloseMenu();

        _menu = _ui.CreateWindow<EmberAccessoryRadialMenu>();
        _menu.OnAccessorySelected += OnAccessorySelected;
        _menu.OnClose += CloseMenu;
        _menu.SetEntity(holder);

        var viewportSize = _clyde.ScreenSize;
        _menu.OpenCenteredAt(_input.MouseScreenPosition.Position / viewportSize);
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        _menu.OnAccessorySelected -= OnAccessorySelected;
        _menu.OnClose -= CloseMenu;

        var menu = _menu;
        _menu = null;
        menu.Dispose();
    }

    private void OnAccessorySelected(EntityUid accessory)
    {
        RaisePredictiveEvent(new EmberAccessoryDetachRequestEvent(GetNetEntity(accessory)));
    }
}
