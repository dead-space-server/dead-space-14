// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Store;

/// <summary>
/// A store listing's product event: buying it starts a game rule (reinforcements called in from an uplink). Sent to the
/// client with the listing, so it lives in shared; only the server acts on it.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class StartGameRuleProductEvent : EntityEventArgs
{
    [DataField(required: true)]
    public EntProtoId Rule;
}
