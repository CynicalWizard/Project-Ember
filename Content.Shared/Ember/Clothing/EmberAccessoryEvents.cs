using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// Raised on a holder while equipment verbs are being gathered for it, whether the verb event
/// arrived directly or relayed through the wearer's inventory.
/// </summary>
/// <remarks>
/// This exists because the directed event bus allows exactly one subscription per
/// component/event pair, and <see cref="EmberAccessorySystem"/> already claims both
/// GetVerbsEvent&lt;EquipmentVerb&gt; and its relayed form on
/// <see cref="EmberAccessoryHolderComponent"/>. Client-side UI hangs off this instead.
/// </remarks>
public sealed class EmberAccessoryGetVerbsEvent : EntityEventArgs
{
    public readonly GetVerbsEvent<EquipmentVerb> Args;

    public EmberAccessoryGetVerbsEvent(GetVerbsEvent<EquipmentVerb> args)
    {
        Args = args;
    }
}

/// <summary>
/// Raised on both the holder and the accessory before an attachment is allowed to happen.
/// Cancel it to veto the attachment.
/// </summary>
/// <remarks>
/// Bay does this with the inline checks in can_attach_accessory(); an event lets other systems
/// (bulk, species restrictions, antag gear) refuse without touching the accessory system.
/// </remarks>
public sealed class EmberAccessoryAttachAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Holder;
    public readonly EntityUid Accessory;
    public readonly EntityUid? User;

    /// <summary>
    /// Set this when cancelling to explain the refusal to the user. Should be a loc string.
    /// </summary>
    public string? Reason;

    public EmberAccessoryAttachAttemptEvent(EntityUid holder, EntityUid accessory, EntityUid? user)
    {
        Holder = holder;
        Accessory = accessory;
        User = user;
    }
}

/// <summary>
/// Raised on the accessory, and then on the holder, once an accessory has been attached.
/// </summary>
public sealed class EmberAccessoryAttachedEvent : EntityEventArgs
{
    public readonly EntityUid Holder;
    public readonly EntityUid Accessory;
    public readonly EntityUid? User;

    public EmberAccessoryAttachedEvent(EntityUid holder, EntityUid accessory, EntityUid? user)
    {
        Holder = holder;
        Accessory = accessory;
        User = user;
    }
}

/// <summary>
/// Raised on the accessory, and then on the holder, once an accessory has been detached.
/// </summary>
public sealed class EmberAccessoryDetachedEvent : EntityEventArgs
{
    public readonly EntityUid Holder;
    public readonly EntityUid Accessory;
    public readonly EntityUid? User;

    public EmberAccessoryDetachedEvent(EntityUid holder, EntityUid accessory, EntityUid? user)
    {
        Holder = holder;
        Accessory = accessory;
        User = user;
    }
}

/// <summary>
/// Sent by the radial menu when the player picks an accessory to take off.
/// </summary>
[Serializable, NetSerializable]
public sealed class EmberAccessoryDetachRequestEvent : EntityEventArgs
{
    public readonly NetEntity Accessory;

    public EmberAccessoryDetachRequestEvent(NetEntity accessory)
    {
        Accessory = accessory;
    }
}
