using Content.Shared.Objectives;
using Content.Shared.DeadSpace.Skills;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CharacterInfo;

[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;

    public RequestCharacterInfoEvent(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly ProtoId<JobPrototype>? Job;
    public readonly Dictionary<string, List<ObjectiveInfo>> Objectives;
    public readonly List<SkillInfo> Skills; // DS14-Skills
    public readonly string? Briefing;

    public CharacterInfoEvent(
        NetEntity netEntity,
        Dictionary<string, List<ObjectiveInfo>> objectives,
        List<SkillInfo> skills, // DS14-Skills
        string? briefing,
        ProtoId<JobPrototype>? job)
    {
        NetEntity = netEntity;
        Objectives = objectives;
        Skills = skills;
        Briefing = briefing;
        Job = job;
    }
}
