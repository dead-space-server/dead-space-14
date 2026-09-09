// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Atmos.Piping;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GasTemperatureValveComponent : Component
{
    public const float MaxThreshold = 4500f;

    [DataField("inlet")]
    public string InletName = "inlet";

    [DataField("outlet")]
    public string OutletName = "outlet";

    [DataField, AutoNetworkedField]
    public float Threshold = Atmospherics.T20C;

    [DataField, AutoNetworkedField]
    public bool PassWhenBelow = true;

    [ViewVariables]
    public bool Open;
}

[Serializable, NetSerializable]
public enum GasTemperatureValveUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GasTemperatureValveChangeThresholdMessage(float threshold) : BoundUserInterfaceMessage
{
    public float Threshold { get; } = threshold;
}

[Serializable, NetSerializable]
public sealed class GasTemperatureValveToggleModeMessage(bool passWhenBelow) : BoundUserInterfaceMessage
{
    public bool PassWhenBelow { get; } = passWhenBelow;
}
