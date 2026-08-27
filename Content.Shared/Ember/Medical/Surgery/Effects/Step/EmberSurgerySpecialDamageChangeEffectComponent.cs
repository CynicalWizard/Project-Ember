using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.Ember.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgerySpecialDamageChangeEffectComponent : Component
{
    [DataField]
    public string DamageType = "";

    [DataField]
    public bool IsConsumable;
}