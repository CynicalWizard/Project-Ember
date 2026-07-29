using Content.Shared.Ember.Materials;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Materials;

public sealed class EmberOreVisualSystem : EntitySystem
{
    private static readonly ResPath OreSprite = new("/Textures/Ember/Objects/Materials/ore.rsi");

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberOreComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, EmberOreComponent component, ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void UpdateVisuals(EntityUid uid, EmberOreComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
        {
            return;
        }

        var state = string.IsNullOrEmpty(material.OreIconOverlay)
            ? "lump"
            : material.OreIconOverlay;

        sprite.LayerSetSprite(0, new SpriteSpecifier.Rsi(OreSprite, state));
        sprite.LayerSetColor(0, material.Color);
    }
}
