// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryInfectionSystem : EntitySystem
{
    private const string Rules = "Default";
    private const string Cures = "Default";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private static readonly SurgeryVerbKind[] Invasive =
    {
        SurgeryVerbKind.Incise,
        SurgeryVerbKind.Retract,
        SurgeryVerbKind.SawBone,
        SurgeryVerbKind.Amputate,
        SurgeryVerbKind.Drill,
        SurgeryVerbKind.Extract,
        SurgeryVerbKind.Insert,
        SurgeryVerbKind.PullEmbedded,
        SurgeryVerbKind.Seat,
    };

    public void Contaminate(EntityUid user, EntityUid patient, SurgerySiteComponent site, SurgeryVerbKind verb, bool done)
    {
        if (!done || !site.Sterile || Array.IndexOf(Invasive, verb) < 0 || RulesFor() is not { Enabled: true } rules)
            return;

        var germs = 0f;
        if (!Wears(user, "gloves", rules.Gloves))
            germs += rules.NoGloves;
        if (!Wears(user, "mask", rules.Mask))
            germs += rules.NoMask;
        if (!Wears(user, "head", rules.Head))
            germs += rules.NoHead;
        if (!OnTable(patient))
            germs += rules.OffTable;

        germs += _lookup.GetEntitiesInRange<PuddleComponent>(Transform(patient).Coordinates, rules.PuddleRange).Count * rules.Puddle;
        site.Germs = Math.Clamp(site.Germs + germs * 0.35f, 0f, 100f);
    }

    public void Settle(EntityUid patient, SurgerySiteComponent site)
    {
        var germs = site.Germs;
        site.Germs = 0f;
        if (!site.Sterile || RulesFor() is not { Enabled: true } rules || germs < rules.Threshold || !LimbWoundRules.IsBody(site.Zone))
            return;

        var chance = Math.Clamp(0.35f + (germs - rules.Threshold) / 30f, 0f, 0.95f);
        if (!_random.Prob(chance))
            return;

        Infect(patient, site.Zone);
    }

    public bool Infect(EntityUid patient, LimbZone zone)
    {
        if (RulesFor() is not { Enabled: true } rules || !LimbWoundRules.IsBody(zone))
            return false;

        var infection = EnsureComp<SurgeryInfectionComponent>(patient);
        if (infection.Rots(zone))
            return false;

        foreach (var pending in infection.Pending)
        {
            if (pending.Zone == zone)
                return false;
        }

        infection.Pending.Add(new SurgeryInfection
        {
            Zone = zone,
            At = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(rules.DelayMin, rules.DelayMax)),
        });
        if (!infection.Latent.Contains(zone))
            infection.Latent.Add(zone);
        Dirty(patient, infection);
        return true;
    }

    public bool CanCure(EntityUid held)
    {
        return CureSolution(held, out _, out _);
    }

    public bool Cure(EntityUid patient, LimbZone zone, EntityUid? held)
    {
        if (held == null || !CureSolution(held.Value, out var solution, out var reagent))
            return false;

        if (!TryComp<SurgeryInfectionComponent>(patient, out var infection) || !infection.Rots(zone))
            return false;

        var cure = CureFor();
        _solutions.RemoveReagent(solution, reagent, FixedPoint2.New(cure?.Amount ?? 5f));
        infection.Rotting.Remove(zone);
        infection.Latent.Remove(zone);
        infection.Pending.RemoveAll(pending => pending.Zone == zone);
        Dirty(patient, infection);
        return true;
    }

    public override void Update(float frameTime)
    {
        var rules = RulesFor();
        if (rules == null)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SurgeryInfectionComponent>();
        while (query.MoveNext(out var uid, out var infection))
        {
            var changed = false;
            for (var i = infection.Pending.Count - 1; i >= 0; i--)
            {
                var pending = infection.Pending[i];
                if (now < pending.At)
                    continue;

                infection.Pending.RemoveAt(i);
                infection.Latent.Remove(pending.Zone);
                if (!infection.Rotting.Contains(pending.Zone))
                    infection.Rotting.Add(pending.Zone);
                _popup.PopupEntity(Loc.GetString("surgery-infection-start"), uid, uid, PopupType.MediumCaution);
                changed = true;
            }

            if (changed)
                Dirty(uid, infection);

            if (infection.Rotting.Count == 0 || now < infection.NextRot)
                continue;

            infection.NextRot = now + TimeSpan.FromSeconds(rules.RotEvery);
            if (!TryComp<LimbBodyComponent>(uid, out var limbs))
                continue;

            foreach (var zone in infection.Rotting)
                Rot(uid, limbs, zone, rules);

            var poison = new DamageSpecifier();
            poison.DamageDict["Poison"] = rules.RotPoison * infection.Rotting.Count;
            _damage.TryChangeDamage(uid, poison, true, false);
        }
    }

    private void Rot(EntityUid uid, LimbBodyComponent limbs, LimbZone zone, SurgerySterilePrototype rules)
    {
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count || limbs.Zones[index].Severed)
            return;

        var dead = limbs.Zones[index].Health - rules.RotHealth <= 0f;
        _limbs.ApplyEdit(uid, zone, new LimbEdit { Health = -rules.RotHealth, Flesh = rules.RotFlesh, SetAtrophy = dead });
        if (dead && (zone == LimbZone.Torso || zone == LimbZone.Head && limbs.HeadVital) && !_mobs.IsDead(uid))
            _limbs.KillByLimbs(uid, limbs);
    }

    private bool CureSolution(EntityUid held, out Entity<Content.Shared.Chemistry.Components.SolutionComponent> solution, out string reagent)
    {
        solution = default;
        reagent = "";
        if (CureFor() is not { } cure || !TryComp<SolutionContainerManagerComponent>(held, out var manager))
            return false;

        foreach (var (_, found) in _solutions.EnumerateSolutions((held, manager)))
        {
            foreach (var id in cure.Reagents)
            {
                if (found.Comp.Solution.GetTotalPrototypeQuantity(id) < FixedPoint2.New(cure.Amount))
                    continue;

                solution = found;
                reagent = id;
                return true;
            }
        }

        return false;
    }

    private bool Wears(EntityUid user, string slot, List<string> allowed)
    {
        if (!_inventory.TryGetSlotEntity(user, slot, out var worn))
            return false;

        return allowed.Count == 0 || MetaData(worn.Value).EntityPrototype is { } proto && allowed.Contains(proto.ID);
    }

    private bool OnTable(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle)
               && buckle.BuckledTo is { } strap
               && HasComp<SurgerySurfaceComponent>(strap);
    }

    private SurgerySterilePrototype? RulesFor()
    {
        return _prototype.TryIndex<SurgerySterilePrototype>(Rules, out var rules) ? rules : null;
    }

    private SurgeryCurePrototype? CureFor()
    {
        return _prototype.TryIndex<SurgeryCurePrototype>(Cures, out var cure) ? cure : null;
    }
}
