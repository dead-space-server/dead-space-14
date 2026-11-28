// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Server.DeadSpace.PlanetSurvey.Npc;

public sealed partial class SurveyLimbAimOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.PlanFinished;

    [DataField]
    public string ZoneKey = SurveyKeys.AimZone;

    [DataField]
    public bool Melee;

    [DataField]
    public bool Loose;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var limbs = _entManager.System<LimbWoundSystem>();
        LimbZone? zone = blackboard.TryGetValue<LimbZone>(ZoneKey, out var picked, _entManager) ? picked : null;

        if (Melee)
        {
            if (_entManager.HasComponent<LimbTargetComponent>(owner))
                limbs.SetTarget(owner, zone);
            return;
        }

        if (!_entManager.TryGetComponent<LimbAimComponent>(owner, out var aim))
            return;

        if (Loose || zone == null)
        {
            if (aim.Aiming)
                limbs.SetAiming(owner, false, aim);
            return;
        }

        aim.CalledZone = zone.Value;
        _entManager.Dirty(owner, aim);
        limbs.SetAiming(owner, true, aim);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return HTNOperatorStatus.Finished;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var limbs = _entManager.System<LimbWoundSystem>();

        if (Melee)
        {
            if (_entManager.HasComponent<LimbTargetComponent>(owner))
                limbs.SetTarget(owner, null);
            return;
        }

        if (_entManager.TryGetComponent<LimbAimComponent>(owner, out var aim) && aim.Aiming)
            limbs.SetAiming(owner, false, aim);
    }
}
