using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Shared.DeviceLinking;

namespace Content.Server.Atmos.Piping.Binary.Components;

[RegisterComponent, Access(typeof(SignalControlledValveSystem))]
public sealed partial class SignalControlledValveComponent : Component
{
    [DataField("openPort")]
    public string OpenPort = "Open";

    [DataField("closePort")]
    public string ClosePort = "Close";

    [DataField("togglePort")]
    public string TogglePort = "Toggle";
}
