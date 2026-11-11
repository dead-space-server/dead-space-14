// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
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
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The needs are the server's: the clients get the levels (see SatietyLevel) from it.
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AnimalNeedsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            // The step is as long as the time since the last one, not the rate: a lagging server must not slow
            // the hunger down.
            var seconds = (float) (comp.UpdateRate + (now - comp.NextUpdate)).TotalSeconds;
            comp.NextUpdate = now + comp.UpdateRate;

            if (_mobState.IsDead(uid))
                continue;

            comp.Satiety.DeprivedMinutes += NeedRules.Tick(ref comp.Satiety.Value, comp.Satiety.DecayPerMinute, seconds);
            comp.Hydration.DeprivedMinutes += NeedRules.Tick(ref comp.Hydration.Value, comp.Hydration.DecayPerMinute, seconds);

            UpdateLevels((uid, comp));

            if (now >= comp.NextDamage)
            {
                comp.NextDamage = now + comp.DamageInterval;
                ApplyDeprivationDamage(uid, comp);
            }
        }
    }

    private void ApplyDeprivationDamage(EntityUid uid, AnimalNeedsComponent comp)
    {
        var total = new DamageSpecifier();
        AddDeprivation(ref total, comp.Satiety);
        AddDeprivation(ref total, comp.Hydration);

        if (!total.Empty)
            _damageable.TryChangeDamage(uid, total, ignoreResistances: true, interruptsDoAfters: false);
    }

    private static void AddDeprivation(ref DamageSpecifier total, NeedState need)
    {
        if (need.DeprivedMinutes > 0f && !need.DeprivationDamage.Empty)
            total += need.DeprivationDamage * need.DeprivedMinutes;

        need.DeprivedMinutes = 0f;
    }

    /// <summary>
    /// Sets the values of the needs by hand (admins, tests): a value that is null is left as it is.
    /// </summary>
    public void SetNeeds(Entity<AnimalNeedsComponent> ent, float? satiety = null, float? hydration = null, float? satietyDecay = null, float? hydrationDecay = null)
    {
        if (satiety is { } s)
            ent.Comp.Satiety.Value = Math.Clamp(s, 0f, ent.Comp.Satiety.Max);

        if (hydration is { } h)
            ent.Comp.Hydration.Value = Math.Clamp(h, 0f, ent.Comp.Hydration.Max);

        if (satietyDecay is { } sd)
            ent.Comp.Satiety.DecayPerMinute = sd;

        if (hydrationDecay is { } hd)
            ent.Comp.Hydration.DecayPerMinute = hd;

        UpdateLevels(ent);
    }

    /// <summary>
    /// Changes when the animal looks for food and how fast it burns it (a growth stage does that): a value that is
    /// null is left as it is.
    /// </summary>
    public void SetSatietyProfile(Entity<AnimalNeedsComponent> ent, float? seekBelow, float? decayPerMinute)
    {
        if (seekBelow is { } seek)
            ent.Comp.Satiety.SeekBelow = seek;

        if (decayPerMinute is { } decay)
            ent.Comp.Satiety.DecayPerMinute = decay;
    }

    /// <summary>
    /// After a meal or a drink the animal does not look for the same again for a while: the stomach has to digest it
    /// before the hunger or the thirst goes down, and the animal must not go round the area searching meanwhile.
    /// A search that found nothing is put off the same way.
    /// </summary>
    public void DelaySearch(Entity<AnimalNeedsComponent> ent, bool food)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.HusbandrySearchCooldown));
        if (food)
            ent.Comp.NextFoodSearch = until;
        else
            ent.Comp.NextWaterSearch = until;
    }

    /// <summary>
    /// Whether the animal may search for food (water) now: the last search or meal was long enough ago.
    /// </summary>
    public bool CanSearch(Entity<AnimalNeedsComponent> ent, bool food)
    {
        return _timing.CurTime >= (food ? ent.Comp.NextFoodSearch : ent.Comp.NextWaterSearch);
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
