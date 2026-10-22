using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Terminal;

[RegisterComponent]
public sealed partial class TaipanAgentCargoComponent : Component
{
    public const string BufferContainerId = "SyndicateAgentBuffer";

    public string? Request;

    public bool HasShipment;

    public Container? Buffer;

    public EntityUid? ExchangeBag;

    public EntityUid? ShipmentTerminal;
}
