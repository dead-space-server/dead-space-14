// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.DeadSpace.Renegade.Components;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.Audio;
using Content.Shared.DeadSpace.Renegade.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Renegade;

public sealed partial class RenegadeRule : StationEventSystem<Renegade.Components.RenegadeRuleComponent>
{
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
    }

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        bool isConditionsComplete = false;

        var queryRenegade = EntityQueryEnumerator<RenegadeComponent>();
        EntityUid RenegadeEntity = default;

        while (queryRenegade.MoveNext(out var RenegadeEnt, out _))
        {
            if (!_mindSystem.TryGetMind(RenegadeEnt, out var mindId, out var mind))
                continue;

            if (mind == null)
                continue;

            foreach (var objId in mind.Objectives)
            {
                if (!HasComp<RenegadeSubmissionConditionComponent>(objId))
                    continue;

                if (_objectives.IsCompleted(objId, (mindId, mind)))
                {
                    isConditionsComplete = true;
                    RenegadeEntity = RenegadeEnt;
                    break;
                }

            }

            if (isConditionsComplete)
                break;
        }

        if (!isConditionsComplete)
            return;

        var msg = new GameGlobalSoundEvent("/Audio/_DeadSpace/Renegade/the_Renegade_has_captured_the_space_station.ogg", AudioParams.Default);
        var stationFilter = RuleStation.GetEventPlayers(RenegadeEntity);
        RaiseNetworkEvent(msg, stationFilter);

        RuleStation.Announce(RenegadeEntity, Loc.GetString("round-end-system-renegade-shuttle-called-announcement"), playSound: true, colorOverride: Color.DarkRed);
    }
}
