// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.Rejuvenate;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// The part of the needs of an animal that both sides and other systems (udder, wool, eggs, breeding) use: reading,
/// changing and paying with satiety and hydration. Draining over time and hurting is the job of the server system.
/// </summary>
public sealed class SharedAnimalNeedsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalNeedsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnimalNeedsComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(Entity<AnimalNeedsComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        RollStartingValue(comp.Satiety);
        RollStartingValue(comp.Hydration);

        // Spread the first update of animals that were spawned together, so they do not all tick on the same frame.
        var jitter = TimeSpan.FromSeconds(_random.NextDouble() * comp.UpdateRate.TotalSeconds);
        comp.NextUpdate = _timing.CurTime + comp.UpdateRate + jitter;
        comp.NextDamage = _timing.CurTime + comp.DamageInterval;
        UpdateLevels(ent);
    }

    private void OnRejuvenate(Entity<AnimalNeedsComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Satiety.Value = ent.Comp.Satiety.Max;
        ent.Comp.Hydration.Value = ent.Comp.Hydration.Max;
        ent.Comp.Satiety.DeprivedMinutes = 0f;
        ent.Comp.Hydration.DeprivedMinutes = 0f;
        UpdateLevels(ent);
    }

    private void RollStartingValue(NeedState need)
    {
        if (need.Value >= 0f)
            return;

        need.Value = Math.Min(need.Max, _random.NextFloat(need.StartingRange.X, need.StartingRange.Y));
    }

    /// <summary>
    /// Adds to the satiety of the animal.
    /// </summary>
    /// <returns>How much it changed.</returns>
    public float ModifySatiety(Entity<AnimalNeedsComponent> ent, float amount)
    {
        var need = ent.Comp.Satiety;
        var change = NeedRules.Modify(ref need.Value, need.Max, amount);
        UpdateLevels(ent);
        return change;
    }

    /// <summary>
    /// Adds to the hydration of the animal.
    /// </summary>
    /// <returns>How much it changed.</returns>
    public float ModifyHydration(Entity<AnimalNeedsComponent> ent, float amount)
    {
        var need = ent.Comp.Hydration;
        var change = NeedRules.Modify(ref need.Value, need.Max, amount);
        UpdateLevels(ent);
        return change;
    }

    /// <summary>
    /// Pays for something the animal makes (milk, wool, eggs, a baby) with its satiety: only if at least
    /// <paramref name="floor"/> is left after paying.
    /// </summary>
    /// <returns>True if it was paid.</returns>
    public bool TryConsumeSatiety(Entity<AnimalNeedsComponent> ent, float amount, float floor = 0f)
    {
        if (!NeedRules.CanAfford(ent.Comp.Satiety.Value, amount, floor))
            return false;

        ModifySatiety(ent, -amount);
        return true;
    }

    /// <summary>
    /// Whether the satiety of the animal is at least <paramref name="fraction"/> of its maximum.
    /// </summary>
    public bool IsSatietyAtLeast(Entity<AnimalNeedsComponent> ent, float fraction)
    {
        return NeedRules.Fraction(ent.Comp.Satiety.Value, ent.Comp.Satiety.Max) >= fraction;
    }

    /// <summary>
    /// Whether the hydration of the animal is at least <paramref name="fraction"/> of its maximum.
    /// </summary>
    public bool IsHydrationAtLeast(Entity<AnimalNeedsComponent> ent, float fraction)
    {
        return NeedRules.Fraction(ent.Comp.Hydration.Value, ent.Comp.Hydration.Max) >= fraction;
    }

    public void UpdateLevels(Entity<AnimalNeedsComponent> ent)
    {
        var comp = ent.Comp;
        var satiety = comp.Satiety.GetLevel();
        var hydration = comp.Hydration.GetLevel();

        if (comp.SatietyLevel == satiety && comp.HydrationLevel == hydration)
            return;

        comp.SatietyLevel = satiety;
        comp.HydrationLevel = hydration;
        Dirty(ent);
    }
}
