using Content.Shared.Ember.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Materials;

public sealed class EmberOreSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberOreComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, EmberOreComponent component, ComponentStartup args)
    {
        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
            return;

        if (!string.IsNullOrEmpty(material.OreName))
            _metaData.SetEntityName(uid, material.OreName);

        if (!string.IsNullOrEmpty(material.OreDescription))
            _metaData.SetEntityDescription(uid, material.OreDescription);
    }
}
