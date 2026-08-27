using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Medical.Surgery;

[Serializable, NetSerializable]
public sealed class EmberSurgeryUiRefreshEvent : EntityEventArgs
{
    public NetEntity Uid { get; }

    public EmberSurgeryUiRefreshEvent(NetEntity uid)
    {
        Uid = uid;
    }
}
