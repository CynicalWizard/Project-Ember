using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgeryPartConditionComponent : Component
{
    [DataField]
    public BodyPartType Part;

    [DataField]
    public BodyPartSymmetry? Symmetry;

    [DataField]
    public bool Inverse;
}