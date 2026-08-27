using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgeryTargetComponent : Component
{
    [DataField]
    public bool CanOperate = true;
}
