using Content.Shared.Body.Organ;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgeryOrganConditionComponent : Component
{
    [DataField]
    public ComponentRegistry? Organ;

    [DataField]
    public bool Inverse;

    [DataField]
    public bool Reattaching;

    [DataField(required: true)]
    public string SlotId = string.Empty;
}
