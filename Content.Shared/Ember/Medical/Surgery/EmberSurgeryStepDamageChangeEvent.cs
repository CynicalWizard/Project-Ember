using Content.Shared.Damage;

namespace Content.Shared.Ember.Medical.Surgery;

/// <summary>
///     Raised on the target entity.
/// </summary>
[ByRefEvent]
public record struct EmberSurgeryStepDamageChangeEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Step);
