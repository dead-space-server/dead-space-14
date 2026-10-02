// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.DeadSpace.MedicalRecords;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.MedicalRecords.Systems;

/// <summary>
/// Handles the "print" button on a single medical case: reads the health conclusion template,
/// fills the shared <c>PaperworkTextSubstitutions</c> plus the per-case values, and spawns the
/// <summary>
/// paper at the console.
/// </summary>
/// </summary>
public sealed class MedicalPrintingSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MedicalRecordsConsoleSystem _console = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Own (component, event) pair on the same MedicalRecordsConsoleKey.Key that
        // MedicalRecordsConsoleSystem already subscribes for other messages - no collision, same
        // reasoning as PersonnelPrintingSystem.
        Subs.BuiEvents<MedicalRecordsConsoleComponent>(MedicalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<MedicalRecordPrintCase>(OnPrintCase);
        });
    }

    private void OnPrintCase(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordPrintCase msg)
    {
        if (!_console.TryCheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < _console.GetNextPrintTime(ent.Comp))
            return;

        if (!_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
            return;

        if (!_console.CanPrint(mob.Value, ent))
            return;

        if (!_records.TryGetRecord<MedicalRecord>(key.Value, out var record))
            return;

        if (msg.Index < 0 || msg.Index >= record.History.Count)
            return;

        PrintCase(ent, general, record, record.History[msg.Index]);
    }

    private void PrintCase(
        Entity<MedicalRecordsConsoleComponent> ent,
        GeneralStationRecord general,
        MedicalRecord record,
        MedicalCase medicalCase)
    {
        if (!_prototype.TryIndex(_console.GetConclusionForm(ent.Comp), out var formPrototype))
            return;

        var text = _resourceManager.ContentFileReadText(formPrototype.Text).ReadToEnd();

        var stationName = _station.GetOwningStation(ent) is { } station ? Name(station) : null;
        text = PaperworkTextSubstitutions.ApplyBase(text, Loc.GetString(formPrototype.Name), _gameTicker.RoundDuration(), stationName);

        text = text.Replace("{{PATIENT.NAME}}", general.Name);
        text = text.Replace("{{PATIENT.SPECIES}}", GetSpeciesName(general.Species));
        text = text.Replace("{{PATIENT.SEX}}", Loc.GetString(SexKey(record.Sex)));
        text = text.Replace("{{PATIENT.DNA}}", general.DNA ?? Loc.GetString("medical-records-print-no-dna"));

        text = text.Replace("{{CASE.KIND}}", Loc.GetString(medicalCase.Kind == MedicalCaseKind.Deviation
            ? "medical-records-case-kind-deviation"
            : "medical-records-case-kind-illness"));
        text = text.Replace("{{CASE.ADDED}}", FormatCaseTime(medicalCase.AddTime));
        text = text.Replace("{{CASE.AUTHOR}}", medicalCase.AuthorName ?? Loc.GetString("medical-records-case-no-author"));
        text = text.Replace("{{CASE.ADMISSION}}", Or(medicalCase.AdmissionState, "medical-records-print-empty"));
        text = text.Replace("{{CASE.DIAGNOSIS}}", Or(medicalCase.Diagnosis, "medical-records-print-empty"));
        text = text.Replace("{{CASE.TREATMENT}}", Or(medicalCase.Treatment, "medical-records-print-empty"));
        text = text.Replace("{{CASE.CONTINUED}}", Loc.GetString(medicalCase.NeedsContinuedTreatment
            ? "medical-records-print-yes"
            : "medical-records-print-no"));
        text = text.Replace("{{CASE.FORCED}}", Loc.GetString(medicalCase.NeedsForcedTreatment
            ? "medical-records-print-yes"
            : "medical-records-print-no"));
        text = text.Replace("{{CASE.SPECIALISTS}}", medicalCase.Specialists.Count == 0
            ? Loc.GetString("medical-records-print-none")
            : string.Join(", ", medicalCase.Specialists));
        text = text.Replace("{{CASE.RECOMMENDATIONS}}", Or(medicalCase.Recommendations, "medical-records-print-empty"));
        text = text.Replace("{{CASE.DISCHARGE}}", Or(medicalCase.DischargeState, "medical-records-print-empty"));

        var printed = Spawn(formPrototype.PaperPrototype, Transform(ent).Coordinates);
        if (TryComp<PaperComponent>(printed, out var paper))
            _paperSystem.SetContent((printed, paper), text);

        _audio.PlayPvs(_console.GetPrintSound(ent.Comp), ent);
        _console.SetNextPrintTime(ent.Comp, _timing.CurTime + _console.GetPrintDelay(ent.Comp));
    }

    private string GetSpeciesName(string species)
    {
        if (_prototype.TryIndex<SpeciesPrototype>(species, out var prototype))
            return Loc.GetString(prototype.Name);

        return Loc.GetString("generic-not-available-shorthand");
    }

    private static string SexKey(Sex sex) => sex switch
    {
        Sex.Male => "medical-records-sex-male",
        Sex.Female => "medical-records-sex-female",
        _ => "medical-records-sex-unsexed",
    };

    // Not static, unlike SexKey: Loc is an instance member of EntitySystem, so anything that
    // resolves a localization id has to be reached through `this`.
    private string FormatCaseTime(TimeSpan time) => time <= TimeSpan.Zero
        ? Loc.GetString("medical-records-case-time-at-birth")
        : time.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    private string Or(string value, string locKey) =>
        string.IsNullOrWhiteSpace(value) ? Loc.GetString(locKey) : value;}
