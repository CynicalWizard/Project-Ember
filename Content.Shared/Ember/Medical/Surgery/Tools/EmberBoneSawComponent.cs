using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberBoneSawComponent : Component, IEmberSurgeryToolComponent
{
    public string ToolName => "a bone saw";
    public bool? Used { get; set; } = null;
    [DataField, AutoNetworkedField]
    public float Speed { get; set; } = 1f;
}
