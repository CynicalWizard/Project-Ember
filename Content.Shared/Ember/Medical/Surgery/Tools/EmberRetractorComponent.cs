using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmberRetractorComponent : Component, IEmberSurgeryToolComponent
{
    public string ToolName => "a retractor";
    public bool? Used { get; set; } = null;
    [DataField]
    public float Speed { get; set; } = 1f;
}
