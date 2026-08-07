using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Storage;

/// <summary>
/// A closet or crate drawn from a <see cref="EmberClosetStylePrototype"/> rather than from its own sheet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralClosetComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmberClosetStylePrototype> Style;

    /// <summary>Repaints one container without giving it a style of its own.</summary>
    [DataField, AutoNetworkedField]
    public Color? Color;
}
