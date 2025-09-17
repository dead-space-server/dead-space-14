using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared.ADT.EmotePanel;

[Serializable, NetSerializable]
public sealed class RequestEmoteMenuEvent : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly List<string> Prototypes;

    public RequestEmoteMenuEvent(NetEntity target, List<string>? prototypes = null)
    {
        Target = target;
        Prototypes = prototypes ?? new List<string>();
    }
}

[Serializable, NetSerializable]
public sealed class PlayEmoteEvent : EntityEventArgs
{
    public readonly ProtoId<EmotePrototype> ProtoId;
    public readonly NetEntity Target;

    public PlayEmoteEvent(ProtoId<EmotePrototype> protoId, NetEntity target)
    {
        ProtoId = protoId;
        Target = target;
    }
}
