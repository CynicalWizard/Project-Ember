using Content.Shared.Ember.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Materials;

public sealed class EmberMaterialStackSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberMaterialStackComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, EmberMaterialStackComponent component, ComponentStartup args)
    {
        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
            return;

        _metaData.SetEntityName(uid, material.DisplayName);
    }
}
