using Content.Shared.DeviceLinking;

namespace Content.Server.DeviceLinking.Components
{
    [RegisterComponent]
    public sealed partial class DoorSignalControlComponent : Component
    {
        [DataField("openPort")]
        public string OpenPort = "Open";

        [DataField("closePort")]
        public string ClosePort = "Close";

        [DataField("togglePort")]
        public string TogglePort = "Toggle";

        [DataField("boltPort")]
        public string InBolt = "DoorBolt";

        [DataField("onOpenPort")]
        public string OutOpen = "DoorStatus";
    }
}
