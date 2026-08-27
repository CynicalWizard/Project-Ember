using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberScalpelComponent : Component, IEmberSurgeryToolComponent
{
    public string ToolName => "a scalpel";
    public bool? Used { get; set; } = null;
    [DataField, AutoNetworkedField]
    public float Speed { get; set; } = 1f;
}
