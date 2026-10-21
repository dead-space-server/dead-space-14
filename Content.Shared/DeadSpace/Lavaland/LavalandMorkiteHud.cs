// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

public enum LavalandMorkiteLink : byte
{
    /// <summary>No bluespace well deployed on lavaland.</summary>
    NoWell,
    /// <summary>The well is not (yet) connected to any extractor.</summary>
    NotConnected,
    /// <summary>Morkite is flowing into the well.</summary>
    Connected,
    /// <summary>The pipeline was cut: nothing flows and the bar slowly leaks.</summary>
    Leaking,
}

/// <summary>Small HUD of the morkite objective: well bar, pipeline integrity, connection. Sent every second.</summary>
[Serializable, NetSerializable]
public sealed class LavalandMorkiteHudEvent(float morkite, float integrity, LavalandMorkiteLink link) : EntityEventArgs
{
    /// <summary>Morkite bar of the fullest well, percent.</summary>
    public float Morkite = morkite;

    /// <summary>Health of the most damaged pipe of the extractor networks, percent.</summary>
    public float Integrity = integrity;

    public LavalandMorkiteLink Link = link;
}
/// <summary>
/// The player closed the morkite panel and clicked something: the server checks whether it is the bluespace well,
/// a morkite extractor or a pipe of their network, and if so opens the panel again.
/// </summary>
[Serializable, NetSerializable]
public sealed class LavalandMorkiteHudReopenRequestEvent(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class LavalandMorkiteHudReopenEvent : EntityEventArgs;
