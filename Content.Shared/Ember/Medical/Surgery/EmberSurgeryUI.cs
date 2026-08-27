using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Medical.Surgery;

[Serializable, NetSerializable]
public enum EmberSurgeryUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class EmberSurgeryBuiState(Dictionary<NetEntity, List<EntProtoId>> choices) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<EntProtoId>> Choices = choices;
}

[Serializable, NetSerializable]
public sealed class EmberSurgeryBuiRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class EmberSurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step, bool isBody) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;

    // Used as a marker for whether or not we're hijacking surgery by applying it on the body itself.
    public readonly bool IsBody = isBody;
}
