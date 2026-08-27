using Robust.Shared.GameObjects;

namespace Content.Shared.Ember.Spawners;

public sealed class SpawnerSpawnedEvent : EntityEventArgs
{
    public EntityUid Entity { get; }

    public bool IsFriendly { get; }
    public SpawnerSpawnedEvent(EntityUid entity, bool isFriendly)
    {
        Entity = entity;
        IsFriendly = isFriendly;
    }
}