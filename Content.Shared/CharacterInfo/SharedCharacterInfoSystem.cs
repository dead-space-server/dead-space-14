using Content.Shared.Objectives;
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
    public readonly string? Briefing;

<<<<<<< ours
    public CharacterInfoEvent(
        NetEntity netEntity,
        Dictionary<string, List<ObjectiveInfo>> objectives,
        string? briefing,
        ProtoId<JobPrototype>? job)
||||||| base
    public CharacterInfoEvent(NetEntity netEntity, string jobTitle, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing)
=======
    public CharacterInfoEvent(NetEntity netEntity, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing, ProtoId<JobPrototype>? job)
>>>>>>> theirs
    {
        NetEntity = netEntity;
        Objectives = objectives;
        Briefing = briefing;
        Job = job;
    }
}
