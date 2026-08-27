using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Surgery;

/// <summary>
///     Allows the entity to do surgery without having to remove clothing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EmberSurgeryIgnoreClothingComponent : Component { }
