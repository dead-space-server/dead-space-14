using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Content.Server.GameTicking.Rules;

using Content.Server.Station.Systems;
using Content.Server.GameTicking;

namespace Content.Server.DeadSpace.GoalGamerules;

public sealed class LoadOnStationMapRuleSystem : StationEventSystem<LoadOnStationMapRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    protected override void Added(EntityUid uid, LoadOnStationMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        // Обязательное поле — путь к гриду
        if (comp.GridPath is not {} gridPath)
        {
            Log.Error($"[LoadOnStationMapRule] No GridPath specified on {ToPrettyString(uid)}");
            ForceEndSelf(uid, rule);
            return;
        }

        // Получаем любую станцию (обычно — основную)
        if (!_station.GetStations().TryGetValue(0, out var station))
        {
            Log.Error("[LoadOnStationMapRule] No station found!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Получаем MapId карты станции
        var stationMap = Transform(station).MapID;

        // Пытаемся загрузить грид на карту станции
        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(stationMap, gridPath, out var result, opts))
        {
            Log.Error($"[LoadOnStationMapRule] Cannot load grid from {gridPath}");
            ForceEndSelf(uid, rule);
            return;
        }

        var grid = result.Value.Owner;

        Log.Info($"[LoadOnStationMapRule] Loaded grid from {gridPath} onto station map {stationMap}");

        // Сообщаем системе геймрулов (если кому-то нужно)
        var ev = new RuleLoadedGridsEvent(stationMap, new List<EntityUid> { grid });
        RaiseLocalEvent(uid, ref ev);

        base.Added(uid, comp, rule, args);
    }
}
