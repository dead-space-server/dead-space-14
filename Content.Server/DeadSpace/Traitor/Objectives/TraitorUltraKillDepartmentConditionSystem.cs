// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;

namespace Content.Server.DeadSpace.Traitor.Objectives;

public sealed partial class TraitorUltraKillDepartmentConditionSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorUltraKillDepartmentConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<TraitorUltraKillDepartmentConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<TraitorUltraKillDepartmentConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<TraitorUltraKillDepartmentConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.TargetMinds.Clear();

        var query = AllEntityQuery<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mindId == args.MindId ||
                !IsDepartmentMind(mindId, ent.Comp) ||
                mind.OwnedEntity == null)
            {
                continue;
            }

            ent.Comp.TargetMinds.Add(mindId);
        }

        if (ent.Comp.TargetMinds.Count < ent.Comp.MinTargets)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.RequiredKills = Math.Max(1, (int) Math.Ceiling(ent.Comp.TargetMinds.Count * ent.Comp.TargetFraction));
    }

    private void OnAfterAssign(Entity<TraitorUltraKillDepartmentConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.RequiredKills)), args.Meta);
        _metaData.SetEntityDescription(
            ent.Owner,
            Loc.GetString(ent.Comp.Description, ("count", ent.Comp.RequiredKills), ("total", ent.Comp.TargetMinds.Count)),
            args.Meta);
    }

    private void OnGetProgress(Entity<TraitorUltraKillDepartmentConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.RequiredKills <= 0)
        {
            args.Progress = 0f;
            return;
        }

        var dead = 0;
        foreach (var targetMindId in ent.Comp.TargetMinds)
        {
            if (!TryComp<MindComponent>(targetMindId, out var targetMind) ||
                targetMind.OwnedEntity == null ||
                _mind.IsCharacterDeadIc(targetMind))
            {
                dead++;
            }
        }

        args.Progress = Math.Clamp(dead / (float) ent.Comp.RequiredKills, 0f, 1f);
    }

    private bool IsDepartmentMind(EntityUid mindId, TraitorUltraKillDepartmentConditionComponent comp)
    {
        if (!_jobs.MindTryGetJobId(mindId, out var jobId) ||
            jobId == null ||
            !_jobs.TryGetAllDepartments(jobId.Value, out var departments))
        {
            return false;
        }

        return departments.Any(department => department.ID == comp.Department.ToString());
    }
}
