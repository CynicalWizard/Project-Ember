using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Ember.Structures;

public sealed class EmberMaterialTintSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberMaterialTintComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberMaterialTintComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(EntityUid uid, EmberMaterialTintComponent component, ComponentStartup args)
    {
        Apply(uid, component);
    }

    private void OnAfterAutoHandleState(EntityUid uid, EmberMaterialTintComponent component, ref AfterAutoHandleStateEvent args)
    {
        Apply(uid, component);
    }

    private void Apply(EntityUid uid, EmberMaterialTintComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !_prototype.TryIndex(component.Material, out EmberWallMaterialPrototype? material))
            return;

        sprite.Color = (component.Color ?? material.Color).WithAlpha(component.Alpha);
    }
}
