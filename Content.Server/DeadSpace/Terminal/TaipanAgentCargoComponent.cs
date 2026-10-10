using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Terminal;

[RegisterComponent]
public sealed partial class TaipanAgentCargoComponent : Component
{
    public const string BufferContainerId = "SyndicateAgentBuffer";

    [DataField]
    public string RequestActionPrototype = "ActionTraitorRequestCargo";

    [DataField]
    public string ReceiveActionPrototype = "ActionTraitorReceiveCargo";

    public string? Request;

    public bool HasShipment;

    public Container? Buffer;

    public EntityUid? RequestActionEntity;

    public EntityUid? ReceiveActionEntity;
}
