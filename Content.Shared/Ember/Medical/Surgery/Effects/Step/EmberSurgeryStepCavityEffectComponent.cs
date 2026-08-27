using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgeryStepCavityEffectComponent : Component
{
    [DataField]
    public string Action = "Insert";
}