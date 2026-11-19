using Content.Server.Objectives.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Virus;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles kill person condition logic and picking random kill targets.
/// </summary>
public sealed partial class KillPersonConditionSystem : EntitySystem
{
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private TargetObjectiveSystem _target = default!;
    [Dependency] private IRobustRandom _random = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillPersonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<MindContainerComponent, EnterCryostorageEvent>(OnEnterCryostorage); // DS14
    }

    private void OnGetProgress(EntityUid uid, KillPersonConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        args.Progress = GetProgress(target.Value, comp.RequireDead, comp.RequireMaroon);
    }

    private float GetProgress(EntityUid target, bool requireDead, bool requireMaroon)
    {
        // deleted or gibbed or something, counts as dead
        if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            return 1f;

        var targetDead = _mind.IsCharacterDeadIc(mind);
        var targetMarooned = !_emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value) || _mind.IsCharacterUnrevivableIc(mind);
        if (!_config.GetCVar(CCVars.EmergencyShuttleEnabled) && requireMaroon)
        {
            requireDead = true;
            requireMaroon = false;
        }

        if (requireDead && !targetDead)
            return 0f;

        // Always failed if the target needs to be marooned and the shuttle hasn't even arrived yet
        if (requireMaroon && !_emergencyShuttle.EmergencyShuttleArrived)
            return 0f;

        // If the shuttle hasn't left, give 50% progress if the target isn't on the shuttle as a "almost there!"
        if (requireMaroon && !_emergencyShuttle.ShuttlesLeft)
            return targetMarooned ? 0.5f : 0f;

        // If the shuttle has already left, and the target isn't on it, 100%
        if (requireMaroon && _emergencyShuttle.ShuttlesLeft)
            return targetMarooned ? 1f : 0f;

        return 1f; // Good job you did it woohoo
    }

    // DS14-start
    private void OnEnterCryostorage(Entity<MindContainerComponent> ent, ref EnterCryostorageEvent args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var oldTargetMindId, out _))
            return;

        var query = EntityQueryEnumerator<KillPersonConditionComponent, TargetObjectiveComponent, PickRandomPersonComponent>();
        while (query.MoveNext(out var objectiveUid, out var kill, out var target, out var picker))
        {
            if (!ShouldRetargetCryostorageTarget(objectiveUid, kill) ||
                target.Target != oldTargetMindId ||
                !TryGetObjectiveOwner(objectiveUid, out var ownerMindId, out var ownerMind))
                continue;

            if (PickReplacementTarget((objectiveUid, picker), ownerMindId, oldTargetMindId) is not { } replacement)
            {
                Log.Warning($"Could not retarget kill objective {ToPrettyString(objectiveUid)} after {ToPrettyString(oldTargetMindId)} entered cryostorage.");
                continue;
            }

            _target.SetTarget(objectiveUid, replacement, target);
            RefreshObjectiveTitle(objectiveUid, ownerMindId, ownerMind);
        }
    }

    private Entity<MindComponent>? PickReplacementTarget(
        Entity<PickRandomPersonComponent> objective,
        EntityUid ownerMindId,
        EntityUid oldTargetMindId)
    {
        var candidates = new HashSet<Entity<MindComponent>>();
        objective.Comp.Pool.FindMinds(candidates, ownerMindId, EntityManager, _mind);
        _mind.FilterMinds(candidates, objective.Comp.Filters, ownerMindId);
        candidates.RemoveWhere(mind => mind.Owner == oldTargetMindId);

        if (candidates.Count == 0)
            return null;

        return _random.Pick(candidates);
    }

    private bool ShouldRetargetCryostorageTarget(EntityUid objective, KillPersonConditionComponent kill)
    {
        if (kill.RequireMaroon)
            return true;

        var prototype = MetaData(objective).EntityPrototype?.ID;
        return prototype != null && prototype.StartsWith("TraitorUltraKill", StringComparison.Ordinal);
    }

    private bool TryGetObjectiveOwner(EntityUid objective, out EntityUid ownerMindId, out MindComponent ownerMind)
    {
        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindId, out var mind))
        {
            if (!mind.Objectives.Contains(objective))
                continue;

            ownerMindId = mindId;
            ownerMind = mind;
            return true;
        }

        ownerMindId = default;
        ownerMind = default!;
        return false;
    }

    private void RefreshObjectiveTitle(EntityUid objective, EntityUid ownerMindId, MindComponent ownerMind)
    {
        if (!TryComp<ObjectiveComponent>(objective, out var objectiveComp))
            return;

        var ev = new ObjectiveAfterAssignEvent(ownerMindId, ownerMind, objectiveComp, MetaData(objective));
        RaiseLocalEvent(objective, ref ev);
    }
    // DS14-end
}
