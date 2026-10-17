// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandNecropolisGateVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum LavalandNecropolisGateState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}

[Serializable, NetSerializable]
public enum LavalandNecropolisGateLayers : byte
{
    Door,
}
