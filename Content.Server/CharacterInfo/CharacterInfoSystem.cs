using Content.Server.DeadSpace.Skill;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CharacterInfo;
using Content.Shared.DeadSpace.Skills;
using Content.Shared.DeadSpace.Skills.Components;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly SkillSystem _skill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        string? briefing = null;
        ProtoId<JobPrototype>? job = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            _jobs.MindTryGetJobId(mindId, out job);

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        // DS14-Skills-Start
        var skills = new List<SkillInfo>();

        if (TryComp<SkillComponent>(entity, out var skillComponent))
        {
            foreach (var skill in skillComponent.Skills)
            {
                var info = _skill.GetSkillInfo(entity, skill.Key);

                if (info != null)
                    skills.Add(info.Value);
            }
        }

        RaiseNetworkEvent(new CharacterInfoEvent(GetNetEntity(entity), objectives, skills, briefing, job), args.SenderSession);
        // DS14-Skills-End
    }
}
