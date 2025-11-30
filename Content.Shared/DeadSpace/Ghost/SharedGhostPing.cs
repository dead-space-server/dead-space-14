//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ghost.SharedGhostPing;

[Serializable, NetSerializable]
public sealed partial class PingMessage : EntityEventArgs
{
    public string ID { get; set; }
    public PingMessage(string id)
    {
        ID = id;
    }
}