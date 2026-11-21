// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Server.Audio;
using Content.Server.CrewManifest;
using Content.Shared.Examine;
using Robust.Shared.Containers;
using Content.Server.DeadSpace.Virus.Components;
using Content.Server.Station.Systems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Paper;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Virus.Components;
using Robust.Server.GameObjects;
using Content.Shared.DeadSpace.TimeWindow;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Virus;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Content.Shared.Body.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Virus.Systems;

public sealed partial class VirusDiagnoserSystem : EntitySystem
{
    private static readonly TimeSpan ConsoleStatusUpdateCooldown = TimeSpan.FromSeconds(5);
    private static readonly HashSet<string> BloodReagentIds = new(StringComparer.Ordinal)
    {
        "Blood",
        "Slime",
        "Sap",
        "ZombieBlood",
        "NecromorfBlood",
        "AcidicBlood"
    };

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private VirusDiagnoserConsoleSystem _console = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private VirusDiagnoserDataServerSystem _dataServer = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private CrewManifestSystem _crewManifest = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    private const string DnaContainerKey = "dna_container_virus_diagnoser";
    private const string FlaskContainerKey = "flask_container_virus_diagnoser";
    public ProtoId<ReagentPrototype> Reagent = "ViralSolution";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirusDiagnoserComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<VirusDiagnoserComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<VirusDiagnoserComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<VirusDiagnoserComponent, EntInsertedIntoContainerMessage>(OnEntInsertCont);
        SubscribeLocalEvent<VirusDiagnoserComponent, EntRemovedFromContainerMessage>(OnEntRemoveCont);
        SubscribeLocalEvent<VirusDiagnoserComponent, ContainerIsRemovingAttemptEvent>(OnContainerRemoveAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VirusDiagnoserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_powerReceiverSystem.IsPowered(uid))
            {
                SetStatus((uid, comp), VirusDiagnoserStatus.Off);
                continue; // без питания ничего не делаем
            }

            // Если был выключен — включаем
            if (comp.Status == VirusDiagnoserStatus.Off)
                SetStatus((uid, comp), VirusDiagnoserStatus.On);

            if (Exists(comp.CurrentSoundEntity) &&
                comp.Status != VirusDiagnoserStatus.Printing &&
                comp.Status != VirusDiagnoserStatus.BloodScanning)
            {
                UpdateConnectedConsoleThrottled((uid, comp));
                continue;
            }

            switch (comp.Status)
            {
                case VirusDiagnoserStatus.Printing:
                    if (_timedWindowSystem.IsExpired(comp.AnimationWindow))
                    {
                        EndPrintingReport((uid, comp));
                        SetStatus((uid, comp), VirusDiagnoserStatus.On);
                    }
                    break;

                case VirusDiagnoserStatus.Scanning:
                    if (!CanScanning((uid, comp)))
                    {
                        SetStatus((uid, comp), VirusDiagnoserStatus.Denial);
                        break;
                    }

                    EndScanVirus((uid, comp));
                    break;

                case VirusDiagnoserStatus.BloodScanning:
                    if (!CanCheckBloodVirus((uid, comp)))
                    {
                        SetStatus((uid, comp), VirusDiagnoserStatus.Denial);
                        break;
                    }

                    if (_timing.CurTime - comp.ScanStartedAt < comp.ScanDuration)
                    {
                        UpdateConnectedConsoleThrottled((uid, comp));
                        break;
                    }

                    EndBloodVirusCheck((uid, comp));
                    break;

                case VirusDiagnoserStatus.GenerateVirus:
                    if (!CanGenerateVirus((uid, comp)))
                    {
                        SetStatus((uid, comp), VirusDiagnoserStatus.Denial);
                        break;
                    }

                    EndGenerateVirus((uid, comp));
                    break;

                case VirusDiagnoserStatus.Denial:
                    SetStatus((uid, comp), VirusDiagnoserStatus.On);
                    break;

                case VirusDiagnoserStatus.Successfully:
                    SetStatus((uid, comp), VirusDiagnoserStatus.On);
                    break;

                case VirusDiagnoserStatus.On:
                default:
                    break;
            }

        }
    }

    private void OnPortDisconnected(Entity<VirusDiagnoserComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port == ent.Comp.VirusDiagnoserPort)
            ent.Comp.ConnectedConsole = null;
    }

    private void OnEntInsertCont(Entity<VirusDiagnoserComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateConnectedConsoleOnSampleModified((ent, ent.Comp), args.Container.ID);
    }

    private void OnEntRemoveCont(Entity<VirusDiagnoserComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateConnectedConsoleOnSampleModified((ent, ent.Comp), args.Container.ID);
    }

    private void OnContainerRemoveAttempt(Entity<VirusDiagnoserComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.Status == VirusDiagnoserStatus.Scanning &&
            args.Container.ID == DnaContainerKey)
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Status == VirusDiagnoserStatus.BloodScanning &&
            args.Container.ID == FlaskContainerKey)
        {
            args.Cancel();
        }
    }

    private void UpdateConnectedConsoleOnSampleModified(Entity<VirusDiagnoserComponent?> ent, string containerId)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (containerId != DnaContainerKey &&
            containerId != FlaskContainerKey)
            return;

        UpdateConnectedConsole((ent, ent.Comp));
    }

    private void OnAnchor(Entity<VirusDiagnoserComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (ent.Comp.ConnectedConsole == null || !TryComp<VirusDiagnoserConsoleComponent>(ent.Comp.ConnectedConsole, out var console))
            return;

        if (args.Anchored)
        {
            _console.RecheckConnections((ent.Comp.ConnectedConsole.Value, console));
            return;
        }

        _console.UpdateUserInterface((ent.Comp.ConnectedConsole.Value, console));
    }

    private void OnExamine(EntityUid uid, VirusDiagnoserComponent component, ExaminedEvent args)
    {
        BaseContainer? container = default!;

        if (_container.TryGetContainer(uid, DnaContainerKey, out container))
        {

            if (container is ContainerSlot slot)
            {
                if (slot.ContainedEntity != null)
                    args.PushMarkup(Loc.GetString("virus-diagnoser-dna-material-attached"));
            }
        }

        if (_container.TryGetContainer(uid, FlaskContainerKey, out container))
        {
            if (container is ContainerSlot slot)
            {
                if (slot.ContainedEntity != null)
                    args.PushMarkup(Loc.GetString("virus-diagnoser-flask-attached"));
            }
        }
    }

    public void StartPrinting(Entity<VirusDiagnoserComponent?> ent, VirusData? data)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (IsBusy(ent.Comp.Status))
            return;

        ent.Comp.VirusDataCPU = data;
        ent.Comp.BloodAnalysisResult = null;
        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Printing);
    }

    public void StartScanVirus(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (IsBusy(ent.Comp.Status))
            return;

        if (!CanScanning((ent, ent.Comp)))
        {
            SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Denial);
            return;
        }

        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Scanning);
    }

    public void StartBloodVirusCheck(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (IsBusy(ent.Comp.Status))
            return;

        if (!TryBuildBloodAnalysisResult((ent, ent.Comp), out var result))
        {
            SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Denial);
            return;
        }

        ent.Comp.VirusDataCPU = null;
        ent.Comp.BloodAnalysisResult = null;
        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.BloodScanning, GetBloodAnalysisDuration(result.VirusData));
    }

    public bool IsBusy(VirusDiagnoserStatus status)
    {
        return status is VirusDiagnoserStatus.Printing
            or VirusDiagnoserStatus.Scanning
            or VirusDiagnoserStatus.BloodScanning
            or VirusDiagnoserStatus.GenerateVirus;
    }

    private void EndPrintingReport(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var paper = Spawn(ent.Comp.Paper, Transform(ent).Coordinates);
        if (!TryComp<PaperComponent>(paper, out var paperComp))
        {
            QueueDel(paper);
            return;
        }

        if (ent.Comp.BloodAnalysisResult is { } bloodAnalysis)
        {
            ent.Comp.BloodAnalysisResult = null;
            PrintBloodAnalysisReport(paper, paperComp, bloodAnalysis);
            return;
        }

        var data = ent.Comp.VirusDataCPU;

        if (data == null)
        {
            var noVirusText = Loc.GetString("virus-report-no-virus");

            _paperSystem.SetContent((paper, paperComp), noVirusText);
            return;
        }

        // Собираем текст отчёта

        // 1) симптомы
        var symptomsText =
            data.ActiveSymptom.Count == 0
                ? Loc.GetString("virus-report-symptoms-none")
                : string.Join(", ", data.ActiveSymptom.Select(symptom =>
                {
                    // Получаем строковый ID,
                    var id = symptom.ToString();

                    // Если нашли прототип — возвращаем Name
                    if (_prototypeManager.TryIndex<VirusSymptomPrototype>(id, out var proto))
                        return proto.Name;

                    // Если прототипа нет — fallback на ToString()
                    return id;
                }));

        // 2) виды (BodyWhitelist)
        string bodyText;
        if (data.BodyWhitelist == null || data.BodyWhitelist.Count == 0)
        {
            bodyText = Loc.GetString("virus-report-body-any");
        }
        else
        {
            var names = new List<string>();
            foreach (var protoId in data.BodyWhitelist)
            {
                if (_prototypeManager.TryIndex(protoId, out BodyPrototype? sp))
                {
                    // используем локализованное имя, если доступно; иначе ID
                    var display = sp?.Name ?? protoId.ToString();
                    names.Add(display);
                }
                else
                {
                    names.Add(protoId.ToString());
                }
            }

            bodyText = string.Join(", ", names);
        }

        // 3) медицина
        string medicineText;
        if (data.MedicineResistance == null || data.MedicineResistance.Count == 0)
        {
            medicineText = Loc.GetString("virus-report-medicine-none");
        }
        else
        {
            var lines = new List<string>();
            foreach (var kvp in data.MedicineResistance)
            {
                var reagentId = kvp.Key;
                var value = kvp.Value;

                if (_prototypeManager.TryIndex(reagentId, out var rp))
                {
                    var reagentName = rp.LocalizedName;
                    lines.Add(Loc.GetString("virus-report-medicine-entry", ("name", reagentName), ("value", value.ToString("0.00"))));
                }
                else
                {
                    lines.Add(Loc.GetString("virus-report-medicine-entry", ("name", reagentId.ToString()), ("value", value.ToString("0.00"))));
                }
            }

            medicineText = string.Join("\n", lines);
        }

        var content = $@"
        [center][b]{Loc.GetString("virus-report-title")}[/b][/center]

        {Loc.GetString("virus-report-strain", ("id", data.StrainId))}

        {Loc.GetString("virus-report-threshold", ("value", data.MaxThreshold.ToString("0.0")))}
        {Loc.GetString("virus-report-infectivity", ("value", (data.Infectivity * 100).ToString("0")))}

        {Loc.GetString("virus-report-damage-when-dead", ("value", data.DamageWhenDead.ToString("0.0")))}
        {Loc.GetString("virus-report-mutation-points", ("value", (data.MutationPoints).ToString("0")))}
        {Loc.GetString("virus-report-regen-threshold", ("value", data.RegenThreshold.ToString("0.0")))}
        {Loc.GetString("virus-report-regen-mutation", ("value", data.RegenMutationPoints.ToString("0.0")))}
        {Loc.GetString("virus-report-milty-price-delete-symptom", ("value", data.MultiPriceDeleteSymptom.ToString("0.0")))}

        {Loc.GetString("virus-report-default-medicine-resistance", ("value", data.DefaultMedicineResistance.ToString("0.00")))}

        {Loc.GetString("virus-report-medicine-header")}
        {medicineText}

        {Loc.GetString("virus-report-symptoms-header")}
        {(string.IsNullOrWhiteSpace(symptomsText) ? Loc.GetString("virus-report-symptoms-none") : symptomsText)}

        {Loc.GetString("virus-report-bodyes-header")}
        {bodyText}

        [small]{Loc.GetString("virus-report-footer")}[/small]
        ";

        _paperSystem.SetContent((paper, paperComp), content);
    }

    private void PrintBloodAnalysisReport(EntityUid paper, PaperComponent paperComp, BloodVirusAnalysisResult result)
    {
        var hasKnownStrain = result.VirusData != null && result.KnownStrainName != null;

        var virusText = result.VirusData == null
            ? Loc.GetString("virus-blood-report-virus-not-detected")
            : hasKnownStrain
                ? Loc.GetString("virus-blood-report-virus-detected", ("id", result.VirusData.StrainId))
                : Loc.GetString("virus-blood-report-virus-detected-unknown");

        var virusDetailsText = result.VirusData == null
            ? string.Empty
            : hasKnownStrain
                ? $@"
        {Loc.GetString("virus-blood-report-known-strain", ("name", result.KnownStrainName!))}

        {Loc.GetString("virus-report-symptoms-header")}
        {GetSymptomsText(result.VirusData)}
        "
                : Loc.GetString("virus-blood-report-known-strain-none");

        var content = $@"
        [center][b]{Loc.GetString("virus-blood-report-title")}[/b][/center]

        {Loc.GetString("virus-blood-report-patient", ("name", result.PatientName))}
        {Loc.GetString("virus-blood-report-dna", ("dna", result.PatientDna))}
        {Loc.GetString("virus-blood-report-blood-types", ("types", result.BloodTypes))}
        {Loc.GetString("virus-blood-report-blood-volume", ("volume", result.BloodVolume))}

        {Loc.GetString("virus-blood-report-disease-status", ("status", result.DiseaseStatus))}
        {virusText}
        {virusDetailsText}

        [small]{Loc.GetString("virus-report-footer")}[/small]
        ";

        _paperSystem.SetContent((paper, paperComp), content);
    }

    private void EndScanVirus(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Successfully);

        if (!_container.TryGetContainer(ent, DnaContainerKey, out var dnaContainer))
            return;

        if (dnaContainer is not ContainerSlot slot)
            return;

        if (slot.ContainedEntity == null)
            return;

        if (!TryComp<VirusDataCollectorComponent>(slot.ContainedEntity, out var dataCol))
            return;

        if (dataCol.Data == null)
            return;

        if (ent.Comp.ConnectedConsole == null || !TryComp<VirusDiagnoserConsoleComponent>(ent.Comp.ConnectedConsole, out var console))
            return;

        if (!TryComp<VirusDiagnoserDataServerComponent>(console.VirusDiagnoserDataServer, out var server))
            return;

        _dataServer.SaveData((console.VirusDiagnoserDataServer.Value, server), dataCol.Data);

        _container.CleanContainer(dnaContainer);

        _console.UpdateUserInterface((ent.Comp.ConnectedConsole.Value, console));
    }

    private void EndGenerateVirus(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Successfully);

        if (ent.Comp.VirusDataCPU == null)
            return;

        if (!_container.TryGetContainer(ent, FlaskContainerKey, out var dnaContainer))
            return;

        if (dnaContainer is not ContainerSlot slot)
            return;

        if (slot.ContainedEntity == null)
            return;

        var ents = _container.EmptyContainer(dnaContainer);

        foreach (var flask in ents)
        {
            if (!TryComp<SolutionContainerManagerComponent>(flask, out var solutionContainerManager))
                continue;

            if (!TryComp<DrawableSolutionComponent>(flask, out var injectable))
                continue;

            var entWrapper = new Entity<DrawableSolutionComponent?, SolutionContainerManagerComponent?>(flask, injectable, solutionContainerManager);

            if (!_solutionContainer.TryGetDrawableSolution(entWrapper, out Entity<SolutionComponent>? solutionEntity, out Solution? solution))
                continue;

            if (solutionEntity != null && solution != null)
            {
                _solutionContainer.TryAddReagent(solutionEntity.Value, Reagent, solution.MaxVolume, out _);

                foreach (var reagent in solution.Contents)
                {
                    if (reagent.Reagent.Prototype != Reagent)
                        continue;

                    List<ReagentData> reagentData = reagent.Reagent.EnsureReagentData();

                    reagentData.RemoveAll(x => x is VirusData);

                    reagentData.Add(ent.Comp.VirusDataCPU);
                }
            }
        }

    }

    public void StartGenerateVirus(Entity<VirusDiagnoserComponent?> ent, VirusData? data = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (IsBusy(ent.Comp.Status))
            return;

        if (!CanGenerateVirus((ent, ent.Comp)) || data == null)
        {
            SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Denial);
            return;
        }

        ent.Comp.VirusDataCPU = data;
        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.GenerateVirus);
    }

    private void UpdateAppearance(Entity<VirusDiagnoserComponent> ent)
    {
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.SetData(ent, VirusDiagnoserVisuals.Status, ent.Comp.Status, appearance);
    }

    private void SetStatus(Entity<VirusDiagnoserComponent?> ent, VirusDiagnoserStatus newStatus, TimeSpan? scanDuration = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Status == newStatus)
            return;

        if (newStatus != VirusDiagnoserStatus.On)
            QueueDel(ent.Comp.CurrentSoundEntity);

        ent.Comp.CurrentSoundEntity = null;

        switch (newStatus)
        {
            case VirusDiagnoserStatus.On:

                break;
            case VirusDiagnoserStatus.Off:
                break;
            case VirusDiagnoserStatus.Printing:
                _timedWindowSystem.Reset(ent.Comp.AnimationWindow);
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.PrintingSound, ent)?.Entity;
                break;
            case VirusDiagnoserStatus.Scanning:
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.ScanningSound, ent)?.Entity;
                break;
            case VirusDiagnoserStatus.BloodScanning:
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.ScanningSound, ent)?.Entity;
                break;
            case VirusDiagnoserStatus.Denial:
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.DenialSound, ent)?.Entity;
                break;
            case VirusDiagnoserStatus.Successfully:
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.SuccessfullySound, ent)?.Entity;
                break;
            case VirusDiagnoserStatus.GenerateVirus:
                ent.Comp.CurrentSoundEntity = _audio.PlayPvs(ent.Comp.GenerateVirusSound, ent)?.Entity;
                break;
            default:

                break;
        }

        ent.Comp.Status = newStatus;
        if (newStatus is VirusDiagnoserStatus.Scanning or VirusDiagnoserStatus.BloodScanning)
        {
            ent.Comp.ScanStartedAt = _timing.CurTime;
            ent.Comp.ScanDuration = scanDuration ?? GetScanDuration(ent.Comp.ScanningSound);
        }
        else
        {
            ent.Comp.ScanStartedAt = TimeSpan.Zero;
            ent.Comp.ScanDuration = TimeSpan.Zero;
        }

        UpdateAppearance((ent, ent.Comp));
        ent.Comp.NextConsoleStatusUpdate = newStatus is VirusDiagnoserStatus.Scanning or VirusDiagnoserStatus.BloodScanning
            ? _timing.CurTime + ConsoleStatusUpdateCooldown
            : TimeSpan.Zero;
        UpdateConnectedConsole((ent, ent.Comp));
    }

    public int GetScanProgress(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;

        if (ent.Comp.Status is not (VirusDiagnoserStatus.Scanning or VirusDiagnoserStatus.BloodScanning) ||
            ent.Comp.ScanDuration <= TimeSpan.Zero)
            return 0;

        var elapsed = _timing.CurTime - ent.Comp.ScanStartedAt;
        var progress = elapsed.TotalSeconds / ent.Comp.ScanDuration.TotalSeconds * 100;
        return Math.Clamp((int)Math.Round(progress), 0, 100);
    }

    private TimeSpan GetScanDuration(SoundSpecifier? sound)
    {
        if (sound == null)
            return TimeSpan.Zero;

        var duration = _audio.GetAudioLength(_audio.ResolveSound(sound));
        return duration + TimeSpan.FromSeconds(SharedAudioSystem.AudioDespawnBuffer);
    }

    private void UpdateConnectedConsoleThrottled(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Status is not (VirusDiagnoserStatus.Scanning or VirusDiagnoserStatus.BloodScanning))
            return;

        if (_timing.CurTime < ent.Comp.NextConsoleStatusUpdate)
            return;

        ent.Comp.NextConsoleStatusUpdate = _timing.CurTime + ConsoleStatusUpdateCooldown;
        UpdateConnectedConsole((ent, ent.Comp));
    }

    private void UpdateConnectedConsole(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.ConnectedConsole == null ||
            !TryComp<VirusDiagnoserConsoleComponent>(ent.Comp.ConnectedConsole, out var console))
            return;

        _console.UpdateUserInterface((ent.Comp.ConnectedConsole.Value, console));
    }

    public bool CanCheckBloodVirus(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!TryGetFlaskSolution(ent, out var solution))
            return false;

        return TryReadBloodSample(solution, out _, out _, out _, out _);
    }

    private bool TryBuildBloodAnalysisResult(Entity<VirusDiagnoserComponent?> ent, out BloodVirusAnalysisResult result)
    {
        result = new BloodVirusAnalysisResult();

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!TryGetFlaskSolution(ent, out var solution))
            return false;

        if (!TryReadBloodSample(solution, out var bloodTypes, out var bloodVolume, out var dna, out var sampleVirus))
            return false;

        var recognitionError = Loc.GetString("virus-blood-report-recognition-error");
        var patientRecognized = false;
        EntityUid? patient = null;
        var patientName = recognitionError;

        if (!string.IsNullOrWhiteSpace(dna))
        {
            var query = EntityQueryEnumerator<DnaComponent>();
            while (query.MoveNext(out var uid, out var dnaComp))
            {
                if (dnaComp.DNA != dna)
                    continue;

                patient = uid;
                patientRecognized = TryGetManifestPatientName(uid, out patientName);
                break;
            }
        }

        var virusData = sampleVirus == null ? null : (VirusData) sampleVirus.Clone();
        var diseaseStatus = recognitionError;

        if (patientRecognized && patient != null)
        {
            if (TryComp<VirusComponent>(patient.Value, out var liveVirus) &&
                (virusData == null || liveVirus.Data.StrainId == virusData.StrainId))
            {
                virusData = (VirusData) liveVirus.Data.Clone();
                diseaseStatus = GetDiseaseStatusWithProgress(
                    "virus-blood-report-disease-active",
                    liveVirus.Data);
            }
            else if (virusData != null)
            {
                diseaseStatus = GetDiseaseStatusWithProgress(
                    "virus-blood-report-disease-sample",
                    virusData);
            }
            else
            {
                diseaseStatus = Loc.GetString("virus-blood-report-disease-clean");
            }
        }

        result.PatientName = patientName;
        result.PatientDna = patientRecognized && !string.IsNullOrWhiteSpace(dna)
            ? dna
            : recognitionError;
        result.BloodTypes = bloodTypes;
        result.BloodVolume = bloodVolume;
        result.VirusData = virusData;
        result.KnownStrainName = virusData == null ? null : GetKnownStrainName((ent, ent.Comp), virusData);
        result.DiseaseStatus = diseaseStatus;

        return true;
    }

    private bool TryGetManifestPatientName(EntityUid patient, out string patientName)
    {
        patientName = Loc.GetString("virus-blood-report-recognition-error");

        var station = _stationSystem.GetOwningStation(patient);
        if (station == null)
            return false;

        var (_, entries) = _crewManifest.GetCrewManifest(station.Value);
        if (entries == null)
            return false;

        var entityName = Name(patient);
        var entry = entries.Entries.FirstOrDefault(entry => entry.Name == entityName);
        if (entry == null)
            return false;

        patientName = string.IsNullOrWhiteSpace(entry.JobTitle)
            ? entry.Name
            : $"{entry.Name} ({entry.JobTitle})";
        return true;
    }

    private bool TryGetFlaskSolution(EntityUid owner, out Solution solution)
    {
        solution = default!;

        if (!_container.TryGetContainer(owner, FlaskContainerKey, out var container))
            return false;

        if (container is not ContainerSlot slot)
            return false;

        if (slot.ContainedEntity is not { } contained)
            return false;

        if (!TryComp<SolutionContainerManagerComponent>(contained, out var solutionContainerManager))
            return false;

        if (!TryComp<DrawableSolutionComponent>(contained, out var drawable))
            return false;

        var wrapper = new Entity<DrawableSolutionComponent?, SolutionContainerManagerComponent?>(
            contained,
            drawable,
            solutionContainerManager);

        if (!_solutionContainer.TryGetDrawableSolution(wrapper, out _, out var foundSolution))
            return false;

        if (foundSolution == null || foundSolution.Contents.Count == 0)
            return false;

        solution = foundSolution;
        return true;
    }

    private bool TryReadBloodSample(
        Solution solution,
        out string bloodTypes,
        out FixedPoint2 bloodVolume,
        out string? dna,
        out VirusData? virusData)
    {
        var foundBloodTypes = new List<string>();
        bloodVolume = FixedPoint2.Zero;
        dna = null;
        virusData = null;

        foreach (var reagent in solution.Contents)
        {
            var reagentId = reagent.Reagent.Prototype;
            if (!IsBloodReagent(reagentId))
                continue;

            bloodVolume += reagent.Quantity;
            foundBloodTypes.Add(GetReagentName(reagentId));

            var dataList = reagent.Reagent.Data;
            if (dataList == null)
                continue;

            foreach (var data in dataList)
            {
                if (data is DnaData dnaData && string.IsNullOrWhiteSpace(dna) && !string.IsNullOrWhiteSpace(dnaData.DNA))
                {
                    dna = dnaData.DNA;
                    continue;
                }

                if (data is VirusData candidate)
                    virusData = SelectStrongerVirus(virusData, candidate);
            }
        }

        bloodTypes = string.Join(", ", foundBloodTypes.Distinct());
        return bloodVolume > FixedPoint2.Zero;
    }

    private bool IsBloodReagent(string reagentId)
    {
        if (BloodReagentIds.Contains(reagentId))
            return true;

        if (!_prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var prototype))
            return false;

        return prototype.Group == "Biological" &&
            (prototype.Parents?.Contains("Blood") == true ||
                reagentId.EndsWith("Blood", StringComparison.Ordinal));
    }

    private string GetReagentName(string reagentId)
    {
        return _prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var prototype)
            ? prototype.LocalizedName
            : reagentId;
    }

    private VirusData SelectStrongerVirus(VirusData? current, VirusData candidate)
    {
        if (current == null)
            return (VirusData) candidate.Clone();

        return GetVirusStrength(candidate) > GetVirusStrength(current)
            ? (VirusData) candidate.Clone()
            : current;
    }

    private TimeSpan GetBloodAnalysisDuration(VirusData? data)
    {
        var strength = data == null ? 0d : GetVirusStrength(data);
        return TimeSpan.FromSeconds(3d + strength * 7d);
    }

    private double GetVirusStrength(VirusData data)
    {
        var symptomCountScore = Math.Clamp(data.ActiveSymptom.Count / 5d, 0d, 1d);
        var dangerScore = 0d;

        foreach (var symptomId in data.ActiveSymptom)
        {
            if (!_prototypeManager.TryIndex(symptomId, out VirusSymptomPrototype? symptom))
                continue;

            dangerScore = Math.Max(dangerScore, (int) symptom.DangerIndicator / 3d);
        }

        return Math.Clamp(dangerScore * 0.7d + symptomCountScore * 0.3d, 0d, 1d);
    }

    private string? GetKnownStrainName(Entity<VirusDiagnoserComponent> ent, VirusData data)
    {
        if (ent.Comp.ConnectedConsole == null ||
            !TryComp<VirusDiagnoserConsoleComponent>(ent.Comp.ConnectedConsole, out var console))
            return null;

        if (console.VirusDiagnoserDataServer == null ||
            !TryComp<VirusDiagnoserDataServerComponent>(console.VirusDiagnoserDataServer, out var server))
            return null;

        return _dataServer.GetData((console.VirusDiagnoserDataServer.Value, server), data.StrainId) == null
            ? null
            : data.StrainId;
    }

    private string GetSymptomsText(VirusData data)
    {
        if (data.ActiveSymptom.Count == 0)
            return Loc.GetString("virus-report-symptoms-none");

        return string.Join(", ", data.ActiveSymptom.Select(symptom =>
        {
            var id = symptom.ToString();

            return _prototypeManager.TryIndex<VirusSymptomPrototype>(id, out var proto)
                ? proto.Name
                : id;
        }));
    }

    private string GetDiseaseStatusWithProgress(string statusKey, VirusData data)
    {
        var progress = GetVirusProgressPercent(data).ToString("0");

        return $"{Loc.GetString(statusKey)}\n{Loc.GetString("virus-blood-report-disease-progress", ("progress", progress))}";
    }

    private float GetVirusProgressPercent(VirusData data)
    {
        if (data.MaxThreshold <= 0)
            return 0f;

        return Math.Clamp(data.Threshold / data.MaxThreshold * 100f, 0f, 100f);
    }

    private void EndBloodVirusCheck(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!TryBuildBloodAnalysisResult((ent, ent.Comp), out var result))
        {
            SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Denial);
            return;
        }

        ent.Comp.VirusDataCPU = null;
        ent.Comp.BloodAnalysisResult = result;
        SetStatus((ent, ent.Comp), VirusDiagnoserStatus.Printing);
    }

    public bool CanScanning(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_container.TryGetContainer(ent, DnaContainerKey, out var dnaContainer))
            return false;

        if (dnaContainer is not ContainerSlot slot)
            return false;

        if (slot.ContainedEntity == null)
            return false;

        return true;
    }

    public bool CanGenerateVirus(Entity<VirusDiagnoserComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_container.TryGetContainer(ent, FlaskContainerKey, out var container))
            return false;

        if (container is not ContainerSlot slot)
            return false;

        if (slot.ContainedEntity == null)
            return false;

        return true;
    }

}
