// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.DeviceLinkVis;

[Serializable, NetSerializable]
public sealed class DeviceLinkVisEnableEvent : EntityEventArgs
{
    public bool Enabled;
}

[Serializable, NetSerializable]
public sealed class DeviceLinkVisStateEvent : EntityEventArgs
{
    public List<DeviceLinkVisLink> Links = new();
}

[Serializable, NetSerializable]
public struct DeviceLinkVisLink
{
    public NetEntity Source;
    public NetEntity Sink;
    public NetCoordinates SourcePos;
    public NetCoordinates SinkPos;
}
