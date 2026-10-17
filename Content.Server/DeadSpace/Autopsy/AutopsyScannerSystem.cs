// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Organ;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.Autopsy;
using Content.Shared.DoAfter;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.PowerCell;
using Content.Shared.Station;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Autopsy;

public sealed class AutopsyScannerSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedStationRecordsSystem _records = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AutopsyScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AutopsyScannerComponent, AutopsyScannerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, AutopsyScannerComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || !HasComp<OrganComponent>(target) || !_cell.HasActivatableCharge(uid, user: args.User))
            return;

        _audio.PlayPvs(component.ScanningSound, uid);

        OpenUserInterface(args.User, uid);

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDelay, new AutopsyScannerDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true
        });
    }

    private void OnDoAfter(EntityUid uid, AutopsyScannerComponent component, DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target || !HasComp<OrganComponent>(target) || !_cell.TryUseActivatableCharge(uid, user: args.User))
            return;

        _audio.PlayPvs(component.ScanningSound, uid);

        UpdateScannedUser(uid, args.Args.User, target, component);
        args.Handled = true;
    }

    private void OpenUserInterface(EntityUid user, EntityUid scanner)
    {
        if (!_uiSystem.HasUi(scanner, AutopsyScannerUiKey.Key))
            return;

        _uiSystem.OpenUi(scanner, AutopsyScannerUiKey.Key, user);
    }

    public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, AutopsyScannerComponent? autopsyScanner)
    {
        if (!Resolve(uid, ref autopsyScanner))
            return;

        if (target is not { } scanned || !_uiSystem.HasUi(uid, AutopsyScannerUiKey.Key))
            return;

        var message = new AutopsyScannerScannedUserMessage(GetNetEntity(scanned))
        {
            EntityName = Name(scanned),
            IdentityText = ResolveIdentity(scanned),
        };

        if (TryComp<OrganDamageRecordComponent>(scanned, out var record))
        {
            foreach (var organRecord in record.Records)
            {
                message.Details.Add(new OrganDamageDetail
                {
                    Type = _prototype.TryIndex<DamageTypePrototype>(organRecord.DamageType, out var proto)
                        ? proto.LocalizedName
                        : organRecord.DamageType.Id,
                    Amount = $"{organRecord.Amount.Float():0.#}",
                    Source = organRecord.Source,
                    Time = organRecord.Time,
                });
            }

            message.Traces.AddRange(record.Traces);
        }

        OpenUserInterface(user, uid);

        _uiSystem.ServerSendUiMessage(uid, AutopsyScannerUiKey.Key, message);
    }

    private string ResolveIdentity(EntityUid target)
    {
        var dna = GetDna(target);

        if (dna != null && _station.GetOwningStation(target) is { } station && ResolveRecord(station, dna) is { } owner)
            return owner;

        if (TryComp<OrganDamageRecordComponent>(target, out var organRecord) && !string.IsNullOrEmpty(organRecord.OwnerName))
            return organRecord.OwnerName;

        return Loc.GetString("autopsy-scanner-identity-unknown");
    }

    private string? ResolveRecord(EntityUid station, string dna)
    {
        foreach (var (_, record) in _records.GetRecordsOfType<GeneralStationRecord>(station))
        {
            if (record.DNA != dna)
                continue;

            return record.JobTitle.Length > 0
                ? Loc.GetString("autopsy-scanner-identity-found-job", ("name", record.Name), ("job", record.JobTitle))
                : record.Name;
        }

        return null;
    }

    private string? GetDna(EntityUid target)
    {
        if (TryComp<DnaComponent>(target, out var dna) && !string.IsNullOrEmpty(dna.DNA))
            return dna.DNA;

        if (!TryComp<ForensicsComponent>(target, out var forensics))
            return null;

        foreach (var trace in forensics.DNAs)
        {
            if (!string.IsNullOrEmpty(trace))
                return trace;
        }

        return null;
    }
}
