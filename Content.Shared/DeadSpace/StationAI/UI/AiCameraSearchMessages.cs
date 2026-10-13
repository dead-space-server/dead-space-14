// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.StationAI.UI;

[Serializable, NetSerializable]
public enum AiCameraSearchType : byte
{
    All,
    Characters,
    Items,
}

[Serializable, NetSerializable]
public enum AiCameraSearchResultType : byte
{
    Character,
    Item,
}

[Serializable, NetSerializable]
public sealed class AiCameraSearchRequestMessage : BoundUserInterfaceMessage
{
    public string Query = string.Empty;
    public AiCameraSearchType Type = AiCameraSearchType.All;
}

[Serializable, NetSerializable]
public sealed class AiCameraJumpToTargetMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
    public TimeSpan SourceTime;
}

[Serializable, NetSerializable]
public sealed class AiCameraSearchResultsState : BoundUserInterfaceState
{
    public List<AiCameraSearchResult> Results = new();
    public string Message = string.Empty;
}

[Serializable, NetSerializable]
public readonly record struct AiCameraSearchResult(
    NetEntity Entity,
    string Name,
    AiCameraSearchResultType Type);

[Serializable, NetSerializable]
public sealed class StationAiTrackEntityNetworkEvent : EntityEventArgs
{
    public NetEntity Target;
    public TimeSpan SourceTime;

    public StationAiTrackEntityNetworkEvent()
    {
    }

    public StationAiTrackEntityNetworkEvent(NetEntity target, TimeSpan sourceTime)
    {
        Target = target;
        SourceTime = sourceTime;
    }
}
