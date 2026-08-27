using Content.Shared.Ember.Medical.Targeting;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Medical.Targeting.Events;

[Serializable, NetSerializable]
public sealed class EmberTargetChangeEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public EmberTargetBodyPart BodyPart { get; }
    public EmberTargetChangeEvent(NetEntity uid, EmberTargetBodyPart bodyPart)
    {
        Uid = uid;
        BodyPart = bodyPart;
    }
}

[Serializable, NetSerializable]
public sealed class EmberTargetIntegrityChangeEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public bool RefreshUi { get; }
    public EmberTargetIntegrityChangeEvent(NetEntity uid, bool refreshUi = true)
    {
        Uid = uid;
        RefreshUi = refreshUi;
    }
}
