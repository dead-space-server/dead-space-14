// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Virus.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Server.Power.EntitySystems;
using System.Linq;
using Content.Shared.DeadSpace.Virus;
using Robust.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Database;
using Content.Server.Research.Disk;
using Content.Shared.DeadSpace.TimeWindow;

namespace Content.Server.DeadSpace.Virus.Systems;

public sealed partial class VirusDiagnoserDataServerSystem : EntitySystem
{
    [Dependency] private VirusDiagnoserConsoleSystem _console = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VirusEvolutionConsoleSystem _evolutionConsoleSystem = default!;
    [Dependency] private TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private VirusSystem _virus = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirusDiagnoserDataServerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VirusDiagnoserDataServerComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<VirusDiagnoserDataServerComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<VirusDiagnoserDataServerComponent, GetVerbsEvent<Verb>>(DoSetObeliskVerbs);
    }

    private void OnInit(EntityUid uid, VirusDiagnoserDataServerComponent component, ComponentInit args)
    {
        _timedWindowSystem.Reset(component.UpdateWindow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VirusDiagnoserDataServerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timedWindowSystem.IsExpired(component.UpdateWindow))
            {
                _timedWindowSystem.Reset(component.UpdateWindow);
                UpdateServer(uid, component);
            }
        }
    }

    private void UpdateServer(EntityUid uid, VirusDiagnoserDataServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var totalPoints = 0;
        foreach (var data in component.StrainData.Values)
        {
            totalPoints += data.ActiveSymptom.Count * component.SymptomsPointsMultiply;
            totalPoints += data.BodyWhitelist.Count * component.BodyPointsMultiply;
        }

        // UpdateConnectedInterfaces не делаем в Update(float frameTime), интерфейс не адаптирован под это
        component.Points += totalPoints;
    }

    public void UpdateConnectedInterfaces(EntityUid uid, VirusDiagnoserDataServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.ConnectedConsole != null &&
            TryComp<VirusDiagnoserConsoleComponent>(component.ConnectedConsole, out var console))
        {
            _console.UpdateUserInterface((component.ConnectedConsole.Value, console));
        }

        if (component.ConnectedEvolutionConsole != null &&
            TryComp<VirusEvolutionConsoleComponent>(component.ConnectedEvolutionConsole, out var evolutionConsole))
        {
            _evolutionConsoleSystem.UpdateUserInterface((component.ConnectedEvolutionConsole.Value, evolutionConsole));
        }
    }

    private void DoSetObeliskVerbs(Entity<VirusDiagnoserDataServerComponent> server, ref GetVerbsEvent<Verb> args)
    {
        if (server.Comp.Points <= 0)
            return;

        AddDiskVerb(server, ref args, 1000);
        AddDiskVerb(server, ref args, 5000);
        AddDiskVerb(server, ref args, 10000);
    }

    private void AddDiskVerb(
    Entity<VirusDiagnoserDataServerComponent> server,
    ref GetVerbsEvent<Verb> args,
    int requestedPoints)
    {
        if (server.Comp.Points <= 0)
            return;

        var actualPoints = Math.Min(server.Comp.Points, requestedPoints);

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(
                "virus-data-server-get-disk-verb-text",
                ("value", actualPoints)),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () =>
            {
                if (server.Comp.Points <= 0)
                    return;

                var pointsToTake = Math.Min(server.Comp.Points, requestedPoints);

                var diskEnt = Spawn(server.Comp.Disk, Transform(server).Coordinates);

                if (!TryComp<ResearchDiskComponent>(diskEnt, out var diskComp))
                {
                    Del(diskEnt);
                    return;
                }

                diskComp.Points = pointsToTake;
                server.Comp.Points -= pointsToTake;

                if (server.Comp.ConnectedEvolutionConsole != null &&
                    TryComp<VirusEvolutionConsoleComponent>(
                        server.Comp.ConnectedEvolutionConsole,
                        out var evolutionConsole))
                {
                    UpdateConnectedInterfaces(server, server.Comp);
                }
            },
            Impact = LogImpact.Medium
        });
    }

    private void OnPortDisconnected(Entity<VirusDiagnoserDataServerComponent> server, ref PortDisconnectedEvent args)
    {
        if (args.Port == server.Comp.VirusDiagnoserDataServerPort)
        {
            var uid = server.Owner;
            Timer.Spawn(0, () => RebuildLinkedConsoles(uid));
        }
    }

    private void RebuildLinkedConsoles(EntityUid uid)
    {
        if (!TryComp<VirusDiagnoserDataServerComponent>(uid, out var comp))
            return;

        comp.ConnectedConsole = null;
        comp.ConnectedEvolutionConsole = null;

        if (!TryComp<DeviceLinkSinkComponent>(uid, out var sink))
        {
            UpdateConnectedInterfaces(uid, comp);
            return;
        }

        foreach (var sourceUid in sink.LinkedSources)
        {
            var links = _deviceLink.GetLinks(sourceUid, uid);
            if (links.Count == 0)
                continue;

            if (TryComp<VirusDiagnoserConsoleComponent>(sourceUid, out var console) &&
                links.Contains((console.VirusDiagnoserDataServerPort, comp.VirusDiagnoserDataServerPort)))
            {
                comp.ConnectedConsole = sourceUid;
            }

            if (TryComp<VirusEvolutionConsoleComponent>(sourceUid, out var evolutionConsole) &&
                links.Contains((evolutionConsole.VirusDiagnoserDataServerPort, comp.VirusDiagnoserDataServerPort)))
            {
                comp.ConnectedEvolutionConsole = sourceUid;
            }
        }

        UpdateConnectedInterfaces(uid, comp);
    }

    private void OnAnchor(Entity<VirusDiagnoserDataServerComponent> server, ref AnchorStateChangedEvent args)
    {
        if (server.Comp.ConnectedConsole != null && TryComp<VirusDiagnoserConsoleComponent>(server.Comp.ConnectedConsole, out var console))
        {

            if (args.Anchored)
            {
                _console.RecheckConnections((server.Comp.ConnectedConsole.Value, console));
                return;
            }

            _console.UpdateUserInterface((server.Comp.ConnectedConsole.Value, console));
        }

        if (server.Comp.ConnectedEvolutionConsole != null && TryComp<VirusEvolutionConsoleComponent>(server.Comp.ConnectedEvolutionConsole, out var evolutionConsole))
        {

            if (args.Anchored)
            {
                _evolutionConsoleSystem.RecheckConnections((server.Comp.ConnectedEvolutionConsole.Value, evolutionConsole));
                return;
            }

            _evolutionConsoleSystem.UpdateUserInterface((server.Comp.ConnectedEvolutionConsole.Value, evolutionConsole));
        }
    }

    public void AddPoints(Entity<VirusDiagnoserDataServerComponent?> server, int points)
    {
        if (!Resolve(server, ref server.Comp, false))
            return;

        server.Comp.Points += points;

        UpdateConnectedInterfaces(server, server.Comp);
    }

    public string? SaveData(Entity<VirusDiagnoserDataServerComponent?> server, VirusData data, bool saveModifiedAsCopy = false)
    {
        if (!Resolve(server, ref server.Comp, false))
            return null;

        if (!_powerReceiverSystem.IsPowered(server))
            return null;

        var dataToSave = (VirusData)data.Clone();

        if (string.IsNullOrWhiteSpace(dataToSave.StrainId))
            dataToSave.StrainId = GenerateUniqueStrainId(server.Comp);

        if (saveModifiedAsCopy)
            dataToSave.StrainId = GetStrainIdForModifiedCopy(server.Comp, dataToSave);

        var timeFormatted = _timing.CurTime.ToString(@"hh\:mm\:ss");

        // ищем существующую запись с таким StrainId
        var existingKey = server.Comp.StrainData.Keys
            .FirstOrDefault(x => x.Strain == dataToSave.StrainId);

        if (existingKey.Strain != null)
            server.Comp.StrainData.Remove(existingKey);

        var record = new VirusStrainRecord(
            dataToSave.StrainId,
            timeFormatted
        );

        server.Comp.StrainData[record] = dataToSave;
        UpdateConnectedInterfaces(server, server.Comp);

        return dataToSave.StrainId;
    }

    private string GetStrainIdForModifiedCopy(VirusDiagnoserDataServerComponent server, VirusData data)
    {
        var existingEntry = server.StrainData
            .FirstOrDefault(kvp => kvp.Key.Strain == data.StrainId);

        if (EqualityComparer<KeyValuePair<VirusStrainRecord, VirusData>>.Default.Equals(existingEntry, default))
            return data.StrainId;

        if (data.Equals(existingEntry.Value))
            return data.StrainId;

        return GenerateUniqueStrainId(server);
    }

    private string GenerateUniqueStrainId(VirusDiagnoserDataServerComponent server)
    {
        string strainId;

        do
        {
            strainId = _virus.GenerateStrainId();
        } while (server.StrainData.Keys.Any(key => key.Strain == strainId));

        return strainId;
    }

    public void DeleteData(Entity<VirusDiagnoserDataServerComponent?> server, string strainId)
    {
        if (!Resolve(server, ref server.Comp, false))
            return;

        if (!_powerReceiverSystem.IsPowered(server))
            return;

        var key = server.Comp.StrainData.Keys
            .FirstOrDefault(k => k.Strain == strainId);

        if (!key.Equals(default(VirusStrainRecord)))
        {
            server.Comp.StrainData.Remove(key);
            UpdateConnectedInterfaces(server, server.Comp);
        }
    }

    public VirusData? GetData(Entity<VirusDiagnoserDataServerComponent?> server, string strainId)
    {
        if (!Resolve(server, ref server.Comp, false))
            return null;

        if (!_powerReceiverSystem.IsPowered(server))
            return null;

        var entry = server.Comp.StrainData
                    .FirstOrDefault(kvp => kvp.Key.Strain == strainId);

        // Проверка: если ключ по умолчанию — значит ничего не найдено
        if (EqualityComparer<KeyValuePair<VirusStrainRecord, VirusData>>.Default.Equals(entry, default))
            return null;

        var data = entry.Value;
        return (VirusData)data.Clone();
    }

    public List<VirusStrainRecord> GetAllStrains(Entity<VirusDiagnoserDataServerComponent?> server)
    {
        if (!Resolve(server, ref server.Comp, false))
            return new List<VirusStrainRecord>();

        return server.Comp.StrainData.Keys.ToList();
    }


}
