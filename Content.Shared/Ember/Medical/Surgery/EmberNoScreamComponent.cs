using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Medical.Surgery;

/// <summary>
///     Prevents the entity from screaming during surgery without having to be asleep.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EmberNoScreamComponent : Component { }
