namespace Content.Shared.Ember.Medical.Surgery.Steps;

[ByRefEvent]
public record struct EmberSurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, EntityUid Surgery, bool Cancelled = false);