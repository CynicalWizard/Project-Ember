using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Doors;

/// <summary>
/// The non-visual half of Bay's material doors: a material transparent enough counts as glass and stops blocking
/// sight, and a luminescent one glows in its own colour.
/// </summary>
/// <remarks>
/// Bay also names the door after its material, because there a door is one type constructed with a material
/// argument. Here each door is its own prototype with a name and a translation already, so renaming at runtime
/// would only throw those away.
/// </summary>
public sealed class EmberProceduralMaterialDoorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, EmberProceduralMaterialDoorComponent component, MapInitEvent args)
    {
        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
            return;

        // Bay flips a door to glass below half opacity, which also means it no longer blocks vision.
        if (EmberMaterialDoorVisuals.IsGlass(material) && TryComp<OccluderComponent>(uid, out var occluder))
        {
            _occluder.SetEnabled(uid, false, occluder);

            if (TryComp<DoorComponent>(uid, out var door))
                door.Occludes = false;
        }

        if (material.Luminescence is not { } luminescence || luminescence <= 0f)
            return;

        var light = _pointLight.EnsureLight(uid);
        _pointLight.SetRadius(uid, luminescence, light);
        _pointLight.SetEnergy(uid, 0.5f, light);
        _pointLight.SetColor(uid, material.Color, light);
        _pointLight.SetCastShadows(uid, false, light);
        _pointLight.SetEnabled(uid, true, light);
    }
}
