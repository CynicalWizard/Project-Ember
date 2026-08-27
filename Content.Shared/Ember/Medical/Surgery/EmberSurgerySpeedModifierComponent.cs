using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgerySpeedModifierComponent : Component
{
    [DataField]
    public float SpeedModifier = 1.5f;
}