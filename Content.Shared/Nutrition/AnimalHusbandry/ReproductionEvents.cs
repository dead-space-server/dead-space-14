namespace Content.Shared.Nutrition.AnimalHusbandry;

[ByRefEvent]
public record struct ReproductionAttemptEvent(EntityUid Partner)
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct ReproductionStartedEvent(EntityUid Partner);

[ByRefEvent]
public record struct BeforeAnimalBirthEvent
{
    public bool Cancelled;
}
