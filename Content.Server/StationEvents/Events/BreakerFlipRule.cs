using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Whitelist;
using JetBrains.Annotations;

namespace Content.Server.StationEvents.Events;

[UsedImplicitly]
public sealed class BreakerFlipRule : StationEventSystem<BreakerFlipRuleComponent>
{
    [Dependency] private readonly ApcSystem _apcSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!; // DS14 - pre-v288 engine

    protected override void Added(EntityUid uid, BreakerFlipRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var str = Loc.GetString("station-event-breaker-flip-announcement", ("data", Loc.GetString($"random-sentience-event-data-{RobustRandom.Next(1, 6)}")));
        stationEvent.StartAnnouncement = str;

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, BreakerFlipRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation, station => _whitelist.IsWhitelistFailOrNull(component.Blacklist, station), rule: uid)) // DS14
            return;

        // DS14-start
        if (StationSystem.GetLargestGrid(chosenStation.Value) is not { } stationGrid)
            return;
        // DS14-end

        var stationApcs = new List<Entity<ApcComponent>>();
        var query = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        while (query.MoveNext(out var apcUid, out var apc, out var xform))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            // DS14-start
            if (xform.GridUid != stationGrid)
                continue;
            // DS14-end

            stationApcs.Add((apcUid, apc));
        }

        var toDisable = Math.Min(RobustRandom.Next(3, 7), stationApcs.Count);
        if (toDisable == 0)
            return;

        RobustRandom.Shuffle(stationApcs);

        for (var i = 0; i < toDisable; i++)
        {
            var apc = stationApcs[i];
            _apcSystem.ApcToggleBreaker(apc, apc);

            var stateString = apc.Comp.MainBreakerEnabled ? "Enabled" : "Disabled";
            AdminLogManager.Add(LogType.ItemConfigure, LogImpact.Medium,
                $"Station event {ToPrettyString(uid):user} set the main breaker state of {ToPrettyString(apc):entity} to {stateString:state}");
        }
    }
}
