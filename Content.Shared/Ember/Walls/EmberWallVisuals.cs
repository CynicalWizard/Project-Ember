using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Walls;

[Serializable, NetSerializable]
public enum EmberWallVisuals : byte
{
    /// <summary>
    /// How far the wall is towards falling apart, from 0 to 1. The threshold it is measured against lives in a
    /// server-only component, so the value is published rather than recomputed on the client.
    /// </summary>
    DamageFraction,
}
