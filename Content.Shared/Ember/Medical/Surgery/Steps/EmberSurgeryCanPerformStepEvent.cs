using Content.Shared.Inventory;

namespace Content.Shared.Ember.Medical.Surgery.Steps;

[ByRefEvent]
public record struct EmberSurgeryCanPerformStepEvent(
    EntityUid User,
    EntityUid Body,
    List<EntityUid> Tools,
    SlotFlags TargetSlots,
    string? Popup = null,
    EmberStepInvalidReason Invalid = EmberStepInvalidReason.None,
    Dictionary<EntityUid, float>? ValidTools = null
) : IInventoryRelayEvent;
