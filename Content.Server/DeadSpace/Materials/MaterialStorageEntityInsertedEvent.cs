namespace Content.Server.DeadSpace.Materials;

[ByRefEvent]
public readonly record struct MaterialStorageEntityInsertedEvent(EntityUid User, EntityUid Inserted);
