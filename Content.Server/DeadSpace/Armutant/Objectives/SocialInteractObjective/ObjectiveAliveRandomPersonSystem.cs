using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

public sealed class ObjectiveAliveRandomPersonSystem : EntitySystem
{
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ObjectiveAlivePersonComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<PickRandomPersonAliveComponent, ObjectiveAssignedEvent>(OnPersonAssigned);
    }

    private void OnGetProgress(EntityUid uid, ObjectiveAlivePersonComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        args.Progress = GetProgress(target.Value, comp.RequireAlive);
    }

    private void OnPersonAssigned(EntityUid uid, PickRandomPersonAliveComponent comp, ref ObjectiveAssignedEvent args)
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
            if (HasComp<ObjectiveAlivePersonComponent>(objective) && TryComp<TargetObjectiveComponent>(objective, out var protect))
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

    private float GetProgress(EntityUid target, bool requireAlive)
    {
        if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            return 0f;

        if (_mind.IsCharacterDeadIc(mind))
            return 0f;

        if (requireAlive)
            return 1f;

        if (!_config.GetCVar(CCVars.EmergencyShuttleEnabled))
            return 1f;

        if (_emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value))
            return 1f;

        if (_emergencyShuttle.ShuttlesLeft)
            return 0f;

        return _emergencyShuttle.EmergencyShuttleArrived ? 0.5f : 1f;
    }
}
