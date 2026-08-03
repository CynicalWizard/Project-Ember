using Content.Shared.Ember.Materials;
using Content.Shared.Stacks;
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
        SubscribeLocalEvent<EmberMaterialStackComponent, StackCountChangedEvent>(OnCountChanged);
    }

    private void OnStartup(EntityUid uid, EmberMaterialStackComponent component, ComponentStartup args)
    {
        if (TryNameByCount(uid, component))
            return;

        if (!component.RenameEntity || string.IsNullOrEmpty(component.Material))
            return;

        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
            return;

        _metaData.SetEntityName(uid, Loc.GetString(material.DisplayName));
    }

    private void OnCountChanged(EntityUid uid, EmberMaterialStackComponent component, StackCountChangedEvent args)
    {
        TryNameByCount(uid, component, args.NewCount);
    }

    /// <summary>
    /// One rod is a rod and two are rods, and the same goes for every other material that comes in a pile. The
    /// count is handed to Fluent rather than picked between here, so a language can decide for itself how many
    /// forms that needs.
    /// </summary>
    private bool TryNameByCount(EntityUid uid, EmberMaterialStackComponent component, int? count = null)
    {
        if (component.CountedName is not { } name)
            return false;

        count ??= CompOrNull<StackComponent>(uid)?.Count ?? 1;

        _metaData.SetEntityName(uid, Loc.GetString(name, ("count", count.Value)));

        if (component.CountedDescription is { } description)
            _metaData.SetEntityDescription(uid, Loc.GetString(description, ("count", count.Value)));

        return true;
    }
}
