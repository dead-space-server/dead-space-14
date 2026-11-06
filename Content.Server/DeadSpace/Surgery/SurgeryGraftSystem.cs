// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Cloning;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryGraftSystem : EntitySystem
{
    private const string Rules = "Default";
    private const string OrganLines = "-organ";
    private const string AppendageLines = "-tail";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private float _accumulator;
    private float _cellsAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, LimbGraftAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<LimbBodyComponent, LimbGraftedEvent>(OnGrafted);
        SubscribeLocalEvent<SurgeryRejectionComponent, LimbSeveredEvent>(OnSevered);
    }

    public void Mark(EntityUid part, EntityUid body)
    {
        if (TerminatingOrDeleted(part) || HasComp<SurgeryOriginComponent>(part) || !TryComp<HumanoidAppearanceComponent>(body, out var look))
            return;

        var origin = AddComp<SurgeryOriginComponent>(part);
        origin.Species = look.Species.Id;
        origin.Dna = CompOrNull<DnaComponent>(body)?.DNA ?? string.Empty;
    }

    public void MarkPrinted(EntityUid part, string species)
    {
        if (TerminatingOrDeleted(part) || HasComp<SurgeryOriginComponent>(part))
            return;

        var origin = AddComp<SurgeryOriginComponent>(part);
        origin.Species = species;
    }

    public bool Accepts(EntityUid body, EntityUid part, bool popup = true)
    {
        if (!TryComp<SurgeryOriginComponent>(part, out var origin) || PartFits(body, origin))
            return true;

        if (TryComp<HumanoidAppearanceComponent>(body, out var look)
            && !HasComp<UncloningComponent>(body)
            && SurgerySpeciesPrototype.LineageOf(_prototype, look.Species.Id) != null
            && SurgerySpeciesPrototype.LineageOf(_prototype, origin.Species) != null)
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString("surgery-graft-refused" + OrganLines), body, PopupType.MediumCaution);
        return false;
    }

    public void Grafted(EntityUid body, EntityUid part, LimbZone zone, string? organ, string? appendage)
    {
        if (TryComp<SurgeryOriginComponent>(part, out var origin) && !PartFits(body, origin))
            Begin(body, origin.Species, zone, organ, appendage);
    }

    private bool PartFits(EntityUid body, SurgeryOriginComponent origin)
    {
        if (origin.Dna.Length > 0 && CompOrNull<DnaComponent>(body)?.DNA == origin.Dna)
            return true;

        return TryComp<HumanoidAppearanceComponent>(body, out var look) && look.Species.Id == origin.Species;
    }

    private void OnAttempt(Entity<LimbBodyComponent> ent, ref LimbGraftAttemptEvent args)
    {
        if (!TryComp<LimbFleshComponent>(args.Part, out var flesh) || flesh.Species.Length == 0)
            return;

        if (SameDna(ent, flesh))
            return;

        if (TryComp<HumanoidAppearanceComponent>(ent, out var machine)
            && flesh.Species == machine.Species.Id
            && SurgerySpeciesPrototype.IsMachine(_prototype, flesh.Species))
            return;

        if (HasComp<ChangelingIdentityComponent>(ent))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var look)
            || flesh.Unstable
            || HasComp<UncloningComponent>(ent)
            || SurgerySpeciesPrototype.LineageOf(_prototype, look.Species.Id) == null
            || SurgerySpeciesPrototype.LineageOf(_prototype, flesh.Species) == null)
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("surgery-graft-refused"), ent, PopupType.MediumCaution);
        }
    }

    private void OnGrafted(Entity<LimbBodyComponent> ent, ref LimbGraftedEvent args)
    {
        if (HasComp<ChangelingIdentityComponent>(ent))
            return;

        if (TryComp<LimbFleshComponent>(args.Part, out var flesh) && flesh.Species.Length > 0 && !SameDna(ent, flesh))
            Begin(ent, flesh.Species, args.Zone, null, null);
    }

    private void Begin(EntityUid body, string foreignSpecies, LimbZone zone, string? organ, string? appendage)
    {
        if (RulesFor() is not { } rules
            || !TryComp<HumanoidAppearanceComponent>(body, out var look)
            || SurgerySpeciesPrototype.LineageOf(_prototype, look.Species.Id) is not { } own
            || SurgerySpeciesPrototype.LineageOf(_prototype, foreignSpecies) is not { } foreign
            || own == foreign)
            return;

        var mutagen = SurgeryGraftPrototype.PairId(rules.Mutagen, own, foreign);
        if (Tolerant(body, mutagen, rules))
            return;

        var rejection = EnsureComp<SurgeryRejectionComponent>(body);
        rejection.Entries.RemoveAll(entry => Same(entry, zone, organ, appendage));
        rejection.Entries.Add(new SurgeryRejection
        {
            Zone = zone,
            Organ = organ,
            Appendage = appendage,
            Since = _timing.CurTime,
            Mutagen = mutagen,
        });
        Sync(body, rejection);
    }

    private static bool Same(SurgeryRejection entry, LimbZone zone, string? organ, string? appendage)
    {
        if (!string.IsNullOrEmpty(organ))
            return entry.Organ == organ;

        if (!string.IsNullOrEmpty(appendage))
            return entry.Appendage == appendage;

        return IsLimb(entry) && entry.Zone == zone;
    }

    private static bool IsLimb(SurgeryRejection entry)
    {
        return string.IsNullOrEmpty(entry.Organ) && string.IsNullOrEmpty(entry.Appendage);
    }

    private static string Line(string key, SurgeryRejection entry)
    {
        if (!string.IsNullOrEmpty(entry.Organ))
            return key + OrganLines;

        return string.IsNullOrEmpty(entry.Appendage) ? key : key + AppendageLines;
    }

    private void OnSevered(Entity<SurgeryRejectionComponent> ent, ref LimbSeveredEvent args)
    {
        var zone = args.Zone;
        var carried = LimbWoundRules.TryCarried(zone, out var child);
        if (ent.Comp.Entries.RemoveAll(entry => IsLimb(entry) && (entry.Zone == zone || carried && entry.Zone == child)) > 0)
            Sync(ent, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        _cellsAccumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator -= 1f;
        if (RulesFor() is not { } rules)
            return;

        if (_cellsAccumulator >= 10f)
        {
            _cellsAccumulator = 0f;
            KeepCells(rules);
        }

        AbsorbAll(rules);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SurgeryRejectionComponent>();
        while (query.MoveNext(out var uid, out var rejection))
        {
            if (rejection.Entries.Count == 0 || _mobs.IsDead(uid))
                continue;

            var changed = false;
            for (var i = rejection.Entries.Count - 1; i >= 0; i--)
            {
                var entry = rejection.Entries[i];
                LimbOrgan? organ = Enum.TryParse<LimbOrgan>(entry.Organ, out var parsed) ? parsed : null;
                if (organ is { } gone && OrganGone(uid, gone)
                    || organ == null && !string.IsNullOrEmpty(entry.Organ) && PartGone(uid, entry.Organ)
                    || !string.IsNullOrEmpty(entry.Appendage) && !HasAppendage(uid, entry.Appendage))
                {
                    rejection.Entries.RemoveAt(i);
                    changed = true;
                    continue;
                }

                var age = (float) (now - entry.Since).TotalSeconds;
                if (Tolerant(uid, entry.Mutagen, rules))
                {
                    if (age <= rules.Early)
                    {
                        rejection.Entries.RemoveAt(i);
                        changed = true;
                        _popup.PopupEntity(Loc.GetString(Line("surgery-graft-accepted", entry)), uid, uid);
                        continue;
                    }

                    if (!entry.Shocked)
                    {
                        entry.Shocked = true;
                        _mobs.ChangeMobState(uid, MobState.Critical);
                        _popup.PopupEntity(Loc.GetString("surgery-graft-too-late"), uid, uid, PopupType.LargeCaution);
                    }
                }

                foreach (var (at, line) in rules.Stages)
                {
                    if (age < at || entry.Stage >= at)
                        continue;

                    entry.Stage = at;
                    _popup.PopupEntity(Loc.GetString(Line(line, entry)), uid, uid, PopupType.MediumCaution);
                }

                if (age >= rules.Black && !entry.Black)
                {
                    entry.Black = true;
                    if (organ is { } dying)
                        _limbs.ApplyOrganEdit(uid, dying, new LimbOrganEdit { SetRuptured = true, Ruptured = true });
                    else if (IsLimb(entry))
                        _limbs.ApplyEdit(uid, entry.Zone, new LimbEdit { SetAtrophy = true });
                }

                if (age > rules.Early)
                {
                    if (organ is { } hurt)
                        _limbs.ApplyOrganEdit(uid, hurt, new LimbOrganEdit { Damage = LimbOrganRules.Rupture * rules.RotHealth * 0.5f });
                    else if (IsLimb(entry))
                        _limbs.ApplyEdit(uid, entry.Zone, new LimbEdit { Health = -LimbWoundRules.HealthFor(entry.Zone, CompOrNull<LimbBodyComponent>(uid)) * rules.RotHealth });

                    var poison = new DamageSpecifier();
                    poison.DamageDict["Poison"] = FixedPoint2.New(rules.Poison);
                    _damage.TryChangeDamage(uid, poison, true, false);
                }

                rejection.Entries[i] = entry;
                if (age >= rules.Death)
                {
                    _mobs.ChangeMobState(uid, MobState.Dead);
                    break;
                }
            }

            if (changed)
                Sync(uid, rejection);
        }
    }

    private bool OrganGone(EntityUid body, LimbOrgan organ)
    {
        var index = (int) organ;
        return !TryComp<LimbBodyComponent>(body, out var limbs)
               || (uint) index >= (uint) limbs.Organs.Count
               || limbs.Organs[index].Missing;
    }

    private bool PartGone(EntityUid body, string part)
    {
        return TryComp<SurgeryPartsGoneComponent>(body, out var lost) && lost.Parts.Contains(part);
    }

    private bool HasAppendage(EntityUid body, string id)
    {
        return _prototype.TryIndex<SurgeryAppendagePrototype>(id, out var appendage)
               && TryComp<HumanoidAppearanceComponent>(body, out var look)
               && look.MarkingSet.Markings.TryGetValue(appendage.Category, out var list)
               && list.Count > 0;
    }

    private void KeepCells(SurgeryGraftPrototype rules)
    {
        var query = EntityQueryEnumerator<LimbBodyComponent, BloodstreamComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var bloodstream, out var look))
        {
            if (HasComp<UncloningComponent>(uid)
                || SurgerySpeciesPrototype.LineageOf(_prototype, look.Species.Id) is not { } lineage
                || !_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out var solution, out var blood))
                continue;

            var cells = rules.Cells + lineage;
            var have = blood.GetTotalPrototypeQuantity(cells);
            if (have >= FixedPoint2.New(rules.CellsKeep))
                continue;

            _solutions.TryAddReagent(solution.Value, cells, FixedPoint2.New(rules.CellsTop) - have, out _);
        }
    }

    private bool SameDna(EntityUid body, LimbFleshComponent flesh)
    {
        return flesh.Dna.Length > 0 && CompOrNull<DnaComponent>(body)?.DNA == flesh.Dna;
    }

    private bool Tolerant(EntityUid body, string mutagen, SurgeryGraftPrototype rules)
    {
        Absorb(body, rules);
        return TryComp<SurgeryGraftToleranceComponent>(body, out var tolerance) && tolerance.Mutagens.Contains(mutagen);
    }

    private void AbsorbAll(SurgeryGraftPrototype rules)
    {
        var query = EntityQueryEnumerator<LimbBodyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            Absorb(uid, rules);
        }
    }

    private void Absorb(EntityUid body, SurgeryGraftPrototype rules)
    {
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream)
            || !_solutions.TryGetSolution(body, bloodstream.BloodSolutionName, out var solution, out var blood))
            return;

        List<string>? taken = null;
        foreach (var reagent in blood.Contents)
        {
            var id = reagent.Reagent.Prototype;
            if (!id.StartsWith(rules.Mutagen, StringComparison.Ordinal) || reagent.Quantity < FixedPoint2.New(rules.Amount))
                continue;

            taken ??= new List<string>();
            taken.Add(id);
        }

        if (taken == null)
            return;

        var tolerance = EnsureComp<SurgeryGraftToleranceComponent>(body);
        foreach (var id in taken)
        {
            _solutions.RemoveReagent(solution.Value, id, blood.GetTotalPrototypeQuantity(id));
            if (tolerance.Mutagens.Add(id))
                _popup.PopupEntity(Loc.GetString("surgery-graft-tolerance"), body, body);
        }
    }

    private void Sync(EntityUid uid, SurgeryRejectionComponent rejection)
    {
        rejection.Zones.Clear();
        foreach (var entry in rejection.Entries)
        {
            if (IsLimb(entry))
                rejection.Zones.Add(entry.Zone);
        }

        Dirty(uid, rejection);
    }

    private SurgeryGraftPrototype? RulesFor()
    {
        return _prototype.TryIndex<SurgeryGraftPrototype>(Rules, out var rules) ? rules : null;
    }
}
