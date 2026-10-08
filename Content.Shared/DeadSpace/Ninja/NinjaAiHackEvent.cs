namespace Content.Shared.DeadSpace.Ninja;

[ByRefEvent]
public record struct NinjaAiHackEvent(EntityUid Ninja, EntityUid Target);