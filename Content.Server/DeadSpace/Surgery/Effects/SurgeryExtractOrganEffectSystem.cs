using Content.Server.DeadSpace.Autopsy;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Autopsy;
using Content.Shared.DeadSpace.Surgery.Effects;
using Content.Shared.DeadSpace.Surgery.Prototypes;
using Content.Shared.Forensics.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Surgery.Effects;

public sealed partial class SurgeryExtractOrganEffectSystem : SurgeryEffectSystem<SurgeryExtractOrganEffect>
{
    private const string DefaultProfile = "default";

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void OnEffect(ref SurgeryEffectEvent<SurgeryExtractOrganEffect> args)
    {
        var patient = args.Context.Patient;

        if (args.Effect.Amount <= 0 || !HasComp<BodyComponent>(patient))
            return;

        var organs = new List<EntityUid>();

        foreach (var (id, _) in _body.GetBodyOrgans(patient))
        {
            organs.Add(id);
        }

        if (organs.Count == 0)
        {
            if (args.Context.Surgeon is { } emptySurgeon)
                _popup.PopupEntity(Loc.GetString("autopsy-extract-no-organs"), patient, emptySurgeon);

            return;
        }

        var corpseDamage = GetDamage(patient);
        var sequence = TryComp<HumanoidDamageSequenceComponent>(patient, out var damageSequence)
            ? damageSequence.DamageSequence
            : null;
        var traces = CollectTraces(patient);

        var count = Math.Min(args.Effect.Amount, organs.Count);

        for (var i = 0; i < count; i++)
        {
            var index = _random.Next(organs.Count);
            var organ = organs[index];
            organs.RemoveAt(index);

            var slot = GetOrganSlot(organ);

            if (!_body.RemoveOrgan(organ))
                continue;

            if (args.Effect.DropAtPatient)
                _transform.SetCoordinates(organ, Transform(patient).Coordinates);

            if (TryComp<DnaComponent>(patient, out var organicDna) && !string.IsNullOrEmpty(organicDna.DNA))
            {
                var organDna = EnsureComp<DnaComponent>(organ);
                organDna.DNA = organicDna.DNA;
                Dirty(organ, organDna);
            }

            var profile = GetProfile(slot);
            ApplyDamage(organ, corpseDamage, profile, args.Effect.DamageTransfer * args.Scale);
            FillRecord(organ, patient, corpseDamage, sequence, profile, traces, args.Effect.DamageTransfer);

            if (args.Context.Surgeon is { } surgeon)
                _popup.PopupEntity(Loc.GetString("autopsy-extract-organ", ("organ", organ)), patient, surgeon);
        }
    }

    private OrganDamageProfilePrototype GetProfile(string? slot)
    {
        if (slot != null && _prototype.TryIndex<OrganDamageProfilePrototype>(slot, out var profile))
            return profile;

        return _prototype.Index<OrganDamageProfilePrototype>(DefaultProfile);
    }

    private string? GetOrganSlot(EntityUid organ)
    {
        var containers = EntityManager.System<SharedContainerSystem>();

        if (!containers.TryGetContainingContainer((organ, null, null), out var container))
            return null;

        var prefix = SharedBodySystem.OrganSlotContainerIdPrefix;
        var id = container.ID;

        return id.StartsWith(prefix) ? id[prefix.Length..] : null;
    }

    private void ApplyDamage(EntityUid organ, DamageSpecifier corpseDamage, OrganDamageProfilePrototype profile, float share)
    {
        var transfer = new DamageSpecifier();

        foreach (var (type, _) in profile.Damages)
        {
            if (!corpseDamage.DamageDict.TryGetValue(type.Id, out var amount) || amount <= 0)
                continue;

            transfer.DamageDict[type.Id] = amount * share;
        }

        if (transfer.Empty)
            return;

        EnsureComp<DamageableComponent>(organ);
        _damageable.TryChangeDamage(organ, transfer, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void FillRecord(
        EntityUid organ,
        EntityUid patient,
        DamageSpecifier corpseDamage,
        HashSet<DamageEntry>? sequence,
        OrganDamageProfilePrototype profile,
        List<string> traces,
        float share)
    {
        var record = EnsureComp<OrganDamageRecordComponent>(organ);
        record.OwnerName = Name(patient);
        record.Records.Clear();

        foreach (var (type, cause) in profile.Damages)
        {
            if (!corpseDamage.DamageDict.TryGetValue(type.Id, out var amount) || amount <= 0)
                continue;

            var taken = amount * share;
            var source = Loc.GetString(cause);
            var time = TimeSpan.Zero;

            if (sequence != null)
            {
                foreach (var entry in sequence)
                {
                    if (entry.DamageType != type.Id)
                        continue;

                    if (entry.TimeOfDamageTake > time)
                        time = entry.TimeOfDamageTake;

                    if (!string.IsNullOrEmpty(entry.SourceName))
                        source = Loc.GetString("autopsy-scanner-source-entity", ("name", entry.SourceName));
                }
            }

            record.Records.Add(new OrganDamageRecord
            {
                DamageType = type,
                Amount = taken,
                Time = time,
                Source = source,
            });
        }

        record.Traces.Clear();
        record.Traces.AddRange(traces);
    }

    private List<string> CollectTraces(EntityUid body)
    {
        var traces = new List<string>();

        if (TryComp<BloodstreamComponent>(body, out var bloodstream) &&
            _solutions.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var blood))
        {
            foreach (var reagent in blood.Contents)
            {
                AddTrace(traces, reagent.Reagent.Prototype);
            }
        }

        foreach (var (organId, _) in _body.GetBodyOrgans(body))
        {
            if (!HasComp<StomachComponent>(organId))
                continue;

            if (!_solutions.TryGetSolution(organId, StomachSystem.DefaultSolutionName, out _, out var stomach))
                continue;

            foreach (var reagent in stomach.Contents)
            {
                AddTrace(traces, reagent.Reagent.Prototype);
            }
        }

        return traces;
    }

    private void AddTrace(List<string> traces, string reagentId)
    {
        if (!_prototype.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            return;

        var name = reagent.LocalizedName;

        if (!traces.Contains(name))
            traces.Add(name);
    }

    private DamageSpecifier GetDamage(EntityUid target)
    {
        return TryComp<DamageableComponent>(target, out var damageable) ? damageable.Damage : new DamageSpecifier();
    }
}
