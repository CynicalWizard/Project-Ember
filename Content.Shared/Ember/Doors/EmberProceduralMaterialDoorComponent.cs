using Content.Shared.Ember.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Doors;

/// <summary>
/// A door made out of a material, the way Bay's <c>/obj/machinery/door/unpowered/simple</c> works: one shared
/// sprite sheet whose state comes from the material's door icon base and whose colour comes from the material,
/// rather than a hand-drawn RSI per material.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberProceduralMaterialDoorComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype> Material;

    [DataField]
    public ResPath Sprite = new("/Textures/Ember/Structures/Doors/MaterialDoors/material_doors.rsi");
}
