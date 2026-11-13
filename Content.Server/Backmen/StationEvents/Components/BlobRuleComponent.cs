using Content.Server.Backmen.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(BlobSpawnRule))]
public sealed partial class BlobSpawnRuleComponent : Component
{
    [DataField("carrierBlobProtos", required: true), ViewVariables(VVAccess.ReadWrite)]
    public List<string> CarrierBlobProtos = new()
    {
        "MobMouseCancer",
        "MobMouse",
        "MobMouse1",
        "MobMouse2"
    };

    [ViewVariables(VVAccess.ReadOnly), DataField("playersPerCarrierBlob")]
    public int PlayersPerCarrierBlob = 30;

    [ViewVariables(VVAccess.ReadOnly), DataField("maxCarrierBlob")]
    public int MaxCarrierBlob = 3;
}
