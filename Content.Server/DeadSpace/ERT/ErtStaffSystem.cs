// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.ERT.Components;
using Content.Server.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server.DeadSpace.ERT;

/// <summary>
/// Система для управления бойцами ERT отряда.
/// При добавлении mind назначает им objective с целью вызова.
/// </summary>
public sealed partial class ErtStaffSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    private const string ErtMissionObjectiveProto = "ErtMissionObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtStaffComponent, MindAddedMessage>(OnMindAddedStaff);
        SubscribeLocalEvent<ErtStaffComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnMindAddedStaff(EntityUid uid, ErtStaffComponent component, MindAddedMessage args)
    {
        AddMissionObjective(uid, component.CallReason);
    }

    private void OnMindRemoved(EntityUid uid, ErtStaffComponent component, MindRemovedMessage args)
    {
        RemoveMissionObjective(uid);
    }

    private void AddMissionObjective(EntityUid uid, string? callReason)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        // Спавним objective и устанавливаем MissionText через компонент
        var objective = Spawn(ErtMissionObjectiveProto);
        var meta = MetaData(objective);

        var total = string.IsNullOrEmpty(callReason)
            ? Loc.GetString("ert-mission-objective-default")
            : callReason;

        _metaData.SetEntityDescription(objective, total, meta);

        _mind.AddObjective(mindId, mind, objective);
    }

    private void RemoveMissionObjective(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (_mind.TryFindObjective((mindId, mind), ErtMissionObjectiveProto, out var objectiveUid))
            _mind.TryRemoveObjective(mindId, mind, mind.Objectives.IndexOf(objectiveUid.Value));
    }

}
