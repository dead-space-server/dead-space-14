using Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

public sealed class ObjectiveKillOneTrySystem : EntitySystem
{
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ObjectiveKillOneTryComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<PickRandomPersonToDieComponent, ObjectiveAssignedEvent>(OnPersonAssigned);
    }

    private void OnGetProgress(EntityUid uid, ObjectiveKillOneTryComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        if (comp.OneTry)
            args.Progress = 1f;
        else
            args.Progress = GetProgress(target.Value, comp.RequireDie);

        if (args.Progress == 1f)
            comp.OneTry = true;
    }

    private void OnPersonAssigned(EntityUid uid, PickRandomPersonToDieComponent comp, ref ObjectiveAssignedEvent args)
    {
        if (!TryComp<TargetObjectiveComponent>(uid, out var target))
        {
            args.Cancelled = true;
            return;
        }

        if (target.Target != null)
            return;

        var allHumans = _mind.GetAliveHumans(args.MindId);

        foreach (var objective in args.Mind.Objectives)
        {
            if (HasComp<ObjectiveKillOneTryComponent>(objective) && TryComp<TargetObjectiveComponent>(objective, out var protect))
            {
                allHumans.RemoveWhere(x => x.Owner == protect.Target);
            }
        }

        if (allHumans.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(uid, _random.Pick(allHumans), target);
    }
    private float GetProgress(EntityUid target, bool requireDie)
    {
        if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            return 1f;

        if (_mind.IsCharacterDeadIc(mind))
            return 1f;

        if (requireDie)
            return 0f;

        return _emergencyShuttle.EmergencyShuttleArrived ? 0.5f : 0f;
    }
}
