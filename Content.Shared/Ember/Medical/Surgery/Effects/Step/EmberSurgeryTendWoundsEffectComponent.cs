using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.Ember.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberSurgeryTendWoundsEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public string MainGroup = "Brute";

    [DataField, AutoNetworkedField]
    public bool IsAutoRepeatable = true;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = default!;

    [DataField, AutoNetworkedField]
    public float HealMultiplier = 0.07f;
}