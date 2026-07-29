using Content.Shared.Ember.Materials;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Ember.Materials;

public sealed class EmberMaterialStackVisualSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberMaterialStackComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, EmberMaterialStackComponent component, ComponentStartup args)
    {
        if (!component.Tint ||
            !TryComp<SpriteComponent>(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
        {
            return;
        }

        sprite.LayerSetColor(0, material.Color);
    }
}
