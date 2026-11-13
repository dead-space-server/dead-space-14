// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Jittering;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Surgery;

public sealed partial class SurgeryAnesthesiaSystem : EntitySystem
{
    private const string Rules = "Default";

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private SleepingSystem _sleeping = default!;

    private readonly Dictionary<EntityUid, Dictionary<string, FixedPoint2>> _seen = new();
    private readonly HashSet<EntityUid> _live = new();
    private float _accumulator;

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator -= 1f;
        if (RulesFor() is not { Enabled: true } rules)
            return;

        _live.Clear();
        var query = EntityQueryEnumerator<LimbBodyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out _, out var bloodstream))
        {
            var clash = Clash(uid, rules);
            if (clash)
            {
                var poison = new DamageSpecifier();
                poison.DamageDict["Poison"] = FixedPoint2.New(rules.ClashPoison);
                _damage.TryChangeDamage(uid, poison, true, false);
            }

            var waking = rules.Wake.Count > 0 && Measure(uid, rules.Wake) >= rules.WakeAmount;
            if (waking)
                Rouse(uid, bloodstream, rules);

            if (!Operating(uid))
                continue;

            Panic(uid);
            _live.Add(uid);
            Hold(uid, bloodstream, rules, !waking);
            if (!clash && !waking && Dose(uid, rules) >= rules.SleepAmount)
                _status.TryUpdateStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(rules.SleepTime));
        }

        foreach (var uid in _seen.Keys.ToList())
        {
            if (!_live.Contains(uid))
                _seen.Remove(uid);
        }
    }

    public bool Numb(EntityUid patient, bool asleep)
    {
        if (RulesFor() is not { Enabled: true } rules)
            return true;

        if (TryComp<HumanoidAppearanceComponent>(patient, out var look)
            && _prototype.TryIndex<SurgerySpeciesPrototype>(look.Species.Id, out var species)
            && species.Painless)
            return true;

        var drunk = Alcohol(patient, rules);
        if (drunk >= rules.AlcoholNumb)
            return true;

        if (Clash(patient, rules))
            return false;

        return asleep || Dose(patient, rules) >= rules.Amount;
    }

    private void Panic(EntityUid patient)
    {
        if (!TryComp<SurgeryPhobiaComponent>(patient, out var phobia)
            || !TryComp<BuckleComponent>(patient, out var buckle)
            || buckle.BuckledTo is not { } strap
            || !HasComp<SurgerySurfaceComponent>(strap)
            || _mobs.IsIncapacitated(patient)
            || Numb(patient, HasComp<SleepingComponent>(patient)))
            return;

        _jitter.DoJitter(patient, TimeSpan.FromSeconds(1.2), true, 10f, 8f);
        phobia.Accumulator += 1f;
        if (phobia.Accumulator < phobia.ScreamEvery)
            return;

        phobia.Accumulator = 0f;
        _chat.TryEmoteWithChat(patient, "Scream", ignoreActionBlocker: true, forceEmote: true);
    }

    private bool Clash(EntityUid patient, SurgeryAnesthesiaPrototype rules)
    {
        var drunk = Alcohol(patient, rules);
        return drunk > rules.AlcoholClash && drunk < rules.AlcoholNumb && Dose(patient, rules) >= rules.Amount;
    }

    private float Alcohol(EntityUid patient, SurgeryAnesthesiaPrototype rules)
    {
        if (rules.Alcohol.Length == 0
            || !TryComp<BloodstreamComponent>(patient, out var bloodstream)
            || !_solutions.TryGetSolution(patient, bloodstream.BloodSolutionName, out _, out var blood))
            return 0f;

        return blood.GetTotalPrototypeQuantity(rules.Alcohol).Float();
    }

    private bool Operating(EntityUid patient)
    {
        if (TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo is { } strap && HasComp<SurgerySurfaceComponent>(strap))
            return true;

        return TryComp<SurgerySiteComponent>(patient, out var site) && !site.Done && site.Layers.Count > 0;
    }

    private float Dose(EntityUid patient, SurgeryAnesthesiaPrototype rules)
    {
        return Measure(patient, rules.Reagents);
    }

    private float Measure(EntityUid patient, List<string> reagents)
    {
        var best = 0f;
        if (TryComp<BloodstreamComponent>(patient, out var bloodstream)
            && _solutions.TryGetSolution(patient, bloodstream.BloodSolutionName, out _, out var blood))
        {
            foreach (var reagent in reagents)
                best = MathF.Max(best, blood.GetTotalPrototypeQuantity(reagent).Float());
        }

        foreach (var lung in _body.GetBodyOrganEntityComps<LungComponent>(patient))
        {
            if (lung.Comp1.Solution is not { } air)
                continue;

            foreach (var reagent in reagents)
                best = MathF.Max(best, air.Comp.Solution.GetTotalPrototypeQuantity(reagent).Float());
        }

        return best;
    }

    private void Rouse(EntityUid patient, BloodstreamComponent bloodstream, SurgeryAnesthesiaPrototype rules)
    {
        if (_mobs.IsIncapacitated(patient))
            return;

        if (rules.WakePurge > 0f && _solutions.TryGetSolution(patient, bloodstream.BloodSolutionName, out var solution, out _))
        {
            foreach (var reagent in rules.Reagents)
                _solutions.RemoveReagent(solution.Value, reagent, FixedPoint2.New(rules.WakePurge));
        }

        _status.TryRemoveStatusEffect(patient, SleepingSystem.StatusEffectForcedSleeping);
        if (HasComp<SleepingComponent>(patient))
            _sleeping.TryWaking(patient, true);
    }

    private void Hold(EntityUid patient, BloodstreamComponent bloodstream, SurgeryAnesthesiaPrototype rules, bool refill)
    {
        if (!_solutions.TryGetSolution(patient, bloodstream.BloodSolutionName, out var solution, out var blood))
            return;

        if (!_seen.TryGetValue(patient, out var last))
        {
            last = new Dictionary<string, FixedPoint2>();
            _seen[patient] = last;
        }

        foreach (var reagent in rules.Reagents)
        {
            var now = blood.GetTotalPrototypeQuantity(reagent);
            if (refill && rules.Hold > 0f && last.TryGetValue(reagent, out var before) && now < before && now > FixedPoint2.Zero)
            {
                var back = (before - now) * rules.Hold;
                _solutions.TryAddReagent(solution.Value, reagent, back, out var added);
                now += added;
            }

            last[reagent] = now;
        }
    }

    public bool Flinch(EntityUid patient, bool asleep, SurgeryStep? step, LimbZone zone)
    {
        if (step is { Flinch: false } || Numb(patient, asleep) || Calm(patient) || RulesFor() is not { } rules)
            return false;

        var chance = TryComp<SurgeryPhobiaComponent>(patient, out var phobia) ? phobia.FlinchChance : rules.FlinchChance;
        if (!_random.Prob(chance))
            return false;

        _jitter.DoJitter(patient, TimeSpan.FromSeconds(0.7), true, 12f, 9f);
        if (rules.FlinchBleed > 0f && LimbWoundRules.IsBody(zone))
            _limbs.ApplyEdit(patient, zone, new LimbEdit { Bleed = rules.FlinchBleed });
        return true;
    }

    public float Shaken(float progress)
    {
        return progress * (RulesFor()?.FlinchProgress ?? 0.5f);
    }

    public void Tear(EntityUid patient, LimbZone zone, bool asleep)
    {
        if (Numb(patient, asleep) || RulesFor() is not { } rules || rules.TearBleed <= 0f || !LimbWoundRules.IsBody(zone))
            return;

        _limbs.ApplyEdit(patient, zone, new LimbEdit { Bleed = rules.TearBleed });
        _jitter.DoJitter(patient, TimeSpan.FromSeconds(1), true, 14f, 10f);
    }

    private bool Calm(EntityUid patient)
    {
        if (RulesFor() is not { } rules || rules.Calm.Count == 0 || !TryComp<ImplantedComponent>(patient, out var implanted))
            return false;

        foreach (var implant in implanted.ImplantContainer.ContainedEntities)
        {
            if (MetaData(implant).EntityPrototype is { } proto && rules.Calm.Contains(proto.ID))
                return true;
        }

        return false;
    }

    private SurgeryAnesthesiaPrototype? RulesFor()
    {
        return _prototype.TryIndex<SurgeryAnesthesiaPrototype>(Rules, out var rules) ? rules : null;
    }
}
