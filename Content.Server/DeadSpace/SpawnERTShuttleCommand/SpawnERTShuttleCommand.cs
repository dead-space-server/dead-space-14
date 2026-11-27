// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.RoundEnd;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.Tag;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.SpawnERTShuttleCommand;

[AdminCommand(AdminFlags.Spawn)]
public sealed partial class SpawnERTShuttleCommand : LocalizedCommands
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private const string DockTagPrefix = "DockCentcommERT";
    private const int DockPairSize = 2;
    private const double DockRotationBucketSize = 0.001;
    private const double DockLineBucketSize = 0.25;
    private static readonly ProtoId<TagPrototype> DefaultDockTag = "DockCentcommERT";

    public override string Command => "ert_spawn_shuttle";
    public override string Description => "Создаёт и стыкует к станции ЦК шаттл в заданный стык ОБР.";
    public override string Help => "ert_spawn_shuttle <шаттл> [dockTag]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        var roundEnd = _entityManager.System<RoundEndSystem>();
        var docking = _entityManager.System<DockingSystem>();
        var shuttleSystem = _entityManager.System<ShuttleSystem>();
        var mapLoader = _entityManager.System<MapLoaderSystem>();

        var centcommMap = roundEnd.GetCentcomm();
        var centcommGrid = roundEnd.GetCentcommGridEntity();

        if (!_entityManager.TryGetComponent(centcommMap, out MapComponent? mapComponent))
        {
            shell.WriteError("Ошибка: Не найден MapComponent у карты ЦК.");
            return;
        }

        if (_entityManager.Deleted(centcommGrid))
        {
            shell.WriteError("Ошибка: ЦК не существует или было удалено.");
            return;
        }

        if (!_prototypeManager.TryIndex(args[0], out ERTShuttlePrototype? shuttlePrototype))
        {
            shell.WriteError("Ошибка: Неверный аргумент.");
            return;
        }

        ProtoId<TagPrototype>? dockTag = args.Length == 2
            ? args[1]
            : shuttlePrototype.DockTag;

        if (dockTag != null && !_prototypeManager.HasIndex<TagPrototype>(dockTag.Value))
        {
            shell.WriteError($"Ошибка: Неверный тег стыка {dockTag.Value}.");
            return;
        }

        var requestedDockTag = dockTag.GetValueOrDefault();
        var useDefaultDockTag = dockTag == null || requestedDockTag.Equals(DefaultDockTag);
        var dockGroups = useDefaultDockTag
            ? GetDefaultDockGroup(centcommGrid.Value, docking)
            : GetDockGroups(centcommGrid.Value, docking, requestedDockTag);

        if (dockGroups.Count == 0)
        {
            shell.WriteError(useDefaultDockTag
                ? $"Ошибка: На ЦК не найдены стыки ОБР с тегом {DefaultDockTag}."
                : $"Ошибка: На ЦК не найден стык ОБР с тегом {requestedDockTag}.");
            return;
        }

        if (useDefaultDockTag)
        {
            dockGroups[0].Docks.RemoveAll(dock => dock.Comp.Docked);

            if (dockGroups[0].Docks.Count == 0)
            {
                shell.WriteError($"Ошибка: Все стыки ОБР {DefaultDockTag} на ЦК заняты.");
                return;
            }
        }
        else
        {
            dockGroups.RemoveAll(group => IsDockGroupOccupied(group.Docks));

            if (dockGroups.Count == 0)
            {
                shell.WriteError($"Ошибка: Стык ОБР {requestedDockTag} занят.");
                return;
            }
        }

        if (!mapLoader.TryLoadGrid(mapComponent.MapId, shuttlePrototype.Path, out var shuttle) ||
            shuttle == null ||
            _entityManager.Deleted(shuttle.Value.Owner))
        {
            shell.WriteError("Ошибка: Шаттл не существует или был удалён.");
            return;
        }

        var shuttleUid = shuttle.Value.Owner;

        if (!_entityManager.HasComponent<ShuttleComponent>(shuttleUid))
        {
            _entityManager.DeleteEntity(shuttleUid);
            shell.WriteError("Ошибка: Не найден ShuttleComponent у заспавненного шаттла.");
            return;
        }

        var shuttleDocks = docking.GetDocks(shuttleUid);
        if (shuttleDocks.Count == 0)
        {
            _entityManager.DeleteEntity(shuttleUid);
            shell.WriteError("Ошибка: Не найдены стыки у заспавненного шаттла.");
            return;
        }

        if (!_entityManager.TryGetComponent(shuttleUid, out TransformComponent? shuttleTransform))
        {
            _entityManager.DeleteEntity(shuttleUid);
            shell.WriteError("Ошибка: Не найден TransformComponent у заспавненного шаттла.");
            return;
        }

        if (!TryGetDockingConfig(shuttleUid, centcommGrid.Value, shuttleDocks, dockGroups, docking, !useDefaultDockTag, out var config))
        {
            _entityManager.DeleteEntity(shuttleUid);
            shell.WriteError("Ошибка: Стыковка не выполнена.");
            return;
        }

        shuttleSystem.FTLDock((shuttleUid, shuttleTransform), config);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var shuttles = _prototypeManager.EnumeratePrototypes<ERTShuttlePrototype>()
                .Select(p => new CompletionOption(p.ID));

            return CompletionResult.FromHintOptions(shuttles, "<шаттл>");
        }

        if (args.Length == 2)
        {
            var dockTags = _prototypeManager.EnumeratePrototypes<TagPrototype>()
                .Where(p => p.ID == DefaultDockTag || IsErtShuttleDockTag(p.ID))
                .Select(p => new CompletionOption(p.ID));

            return CompletionResult.FromHintOptions(dockTags, "<dockTag>");
        }

        return CompletionResult.Empty;
    }

    private List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)> GetDockGroups(
        EntityUid grid,
        DockingSystem docking,
        ProtoId<TagPrototype> requestedTag)
    {
        var docks = new List<Entity<DockingComponent>>();

        foreach (var dock in docking.GetDocks(grid))
        {
            if (!_entityManager.TryGetComponent(dock.Owner, out TagComponent? tagComponent))
            {
                continue;
            }

            if (!tagComponent.Tags.Any(tag => tag.Equals(requestedTag)))
                continue;

            docks.Add(dock);
        }

        if (docks.Count == 1)
            return new List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)>
            {
                (requestedTag, docks)
            };

        var dockLines = new Dictionary<(int Rotation, int Line), List<(Entity<DockingComponent> Dock, double SortKey)>>();

        foreach (var dock in docks)
        {
            if (!_entityManager.TryGetComponent(dock.Owner, out TransformComponent? transform))
                continue;

            var key = GetDockLineKey(transform);
            var sortKey = GetDockSortKey(transform);

            if (!dockLines.TryGetValue(key, out var lineDocks))
            {
                lineDocks = new List<(Entity<DockingComponent>, double)>();
                dockLines.Add(key, lineDocks);
            }

            lineDocks.Add((dock, sortKey));
        }

        var groups = new List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)>();

        foreach (var dockLine in dockLines
            .OrderBy(group => group.Key.Rotation)
            .ThenBy(group => group.Key.Line))
        {
            var lineDocks = dockLine.Value
                .OrderBy(dock => dock.SortKey)
                .Select(dock => dock.Dock)
                .ToList();

            for (var i = 0; i + DockPairSize - 1 < lineDocks.Count; i += DockPairSize)
            {
                groups.Add((requestedTag, lineDocks
                    .Skip(i)
                    .Take(DockPairSize)
                    .ToList()));
            }
        }

        return groups;
    }

    private List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)> GetDefaultDockGroup(
        EntityUid grid,
        DockingSystem docking)
    {
        var docks = new List<Entity<DockingComponent>>();

        foreach (var dock in docking.GetDocks(grid))
        {
            if (!_entityManager.TryGetComponent(dock.Owner, out PriorityDockComponent? priorityDock) ||
                priorityDock.Tag != DefaultDockTag.Id)
            {
                continue;
            }

            docks.Add(dock);
        }

        return docks.Count == 0
            ? new List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)>()
            : new List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)>
            {
                (DefaultDockTag, docks)
            };
    }

    private static bool IsDockGroupOccupied(List<Entity<DockingComponent>> docks)
    {
        return docks.Any(dock => dock.Comp.Docked);
    }

    private static bool IsErtShuttleDockTag(string tag)
    {
        return tag.StartsWith(DockTagPrefix) && tag != DockTagPrefix;
    }

    private static (int Rotation, int Line) GetDockLineKey(TransformComponent transform)
    {
        var angle = transform.LocalRotation.Reduced().Theta;
        var normal = new Vector2((float) -Math.Sin(angle), (float) Math.Cos(angle));
        var line = Vector2.Dot(transform.LocalPosition, normal);

        return (GetBucket(angle, DockRotationBucketSize), GetBucket(line, DockLineBucketSize));
    }

    private static double GetDockSortKey(TransformComponent transform)
    {
        var angle = transform.LocalRotation.Reduced().Theta;
        var tangent = new Vector2((float) Math.Cos(angle), (float) Math.Sin(angle));

        return Vector2.Dot(transform.LocalPosition, tangent);
    }

    private static int GetBucket(double value, double bucketSize)
    {
        return (int) Math.Round(value / bucketSize);
    }

    private static bool TryGetDockingConfig(
        EntityUid shuttleUid,
        EntityUid targetGrid,
        List<Entity<DockingComponent>> shuttleDocks,
        List<(ProtoId<TagPrototype> Tag, List<Entity<DockingComponent>> Docks)> targetGroups,
        DockingSystem docking,
        bool requireAllTargetDocks,
        [NotNullWhen(true)] out DockingConfig? config)
    {
        foreach (var (_, targetDocks) in targetGroups)
        {
            if (requireAllTargetDocks && IsDockGroupOccupied(targetDocks))
                continue;

            config = docking.GetDockingConfig(shuttleUid, targetGrid, shuttleDocks, targetDocks);

            if (config != null && (!requireAllTargetDocks || UsesAllTargetDocks(config, targetDocks)))
                return true;
        }

        config = null;
        return false;
    }

    private static bool UsesAllTargetDocks(DockingConfig config, List<Entity<DockingComponent>> targetDocks)
    {
        return targetDocks.All(targetDock =>
            config.Docks.Any(docked => docked.DockBUid == targetDock.Owner));
    }
}
