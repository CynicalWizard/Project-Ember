using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Doors;

[Prototype("emberAirlockStyle")]
public sealed partial class EmberAirlockStylePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField]
    public Color? DoorColor;

    [DataField]
    public ProtoId<DepartmentPrototype>? DoorDepartment;

    [DataField]
    public Color? StripeColor;

    [DataField]
    public ProtoId<DepartmentPrototype>? StripeDepartment;

    [DataField]
    public Color? WindowColor;

    [DataField]
    public ProtoId<DepartmentPrototype>? WindowDepartment;
}
