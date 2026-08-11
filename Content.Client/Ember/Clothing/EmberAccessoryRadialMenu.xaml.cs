using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Ember.Clothing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Ember.Clothing;

/// <summary>
/// Lets the player pick which accessory to pull off a piece of clothing.
/// </summary>
/// <remarks>
/// SierraBay12 shows the same choice through show_radial_menu() in its "Remove Accessory" verb
/// (code/modules/clothing/clothing_accessories.dm), so this is the same interaction, built on the
/// engine's <see cref="RadialMenu"/> instead.
/// </remarks>
public sealed partial class EmberAccessoryRadialMenu : RadialMenu
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    /// <summary>
    /// Raised with the accessory the player picked.
    /// </summary>
    public event Action<EntityUid>? OnAccessorySelected;

    public EntityUid Entity { get; private set; }

    public EmberAccessoryRadialMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
    }

    public void SetEntity(EntityUid uid)
    {
        Entity = uid;
        RefreshUI();
    }

    private void RefreshUI()
    {
        var main = FindControl<RadialContainer>("Main");

        if (!_entityManager.TryGetComponent<EmberAccessoryHolderComponent>(Entity, out var holder)
            || holder.Container is not { } container)
        {
            return;
        }

        foreach (var accessory in container.ContainedEntities)
        {
            if (!_entityManager.TryGetComponent<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if ((comp.Flags & EmberAccessoryFlags.Removable) == 0)
                continue;

            var button = new EmberAccessoryRadialMenuButton
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64, 64),
                ToolTip = Loc.GetString("ember-accessory-radial-tooltip",
                    ("accessory", _entityManager.GetComponent<MetaDataComponent>(accessory).EntityName)),
                Accessory = accessory,
            };

            var spriteView = new SpriteView
            {
                SetSize = new Vector2(48, 48),
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Center,
                Stretch = SpriteView.StretchMode.Fill,
            };

            spriteView.SetEntity(accessory);

            button.AddChild(spriteView);
            main.AddChild(button);
        }

        AddButtonClickActions(main);
    }

    private void AddButtonClickActions(RadialContainer main)
    {
        foreach (var child in main.Children)
        {
            if (child is not EmberAccessoryRadialMenuButton button)
                continue;

            button.OnButtonDown += _ =>
            {
                OnAccessorySelected?.Invoke(button.Accessory);
                Close();
            };
        }
    }
}

public sealed class EmberAccessoryRadialMenuButton : RadialMenuTextureButton
{
    public EntityUid Accessory { get; set; }
}
