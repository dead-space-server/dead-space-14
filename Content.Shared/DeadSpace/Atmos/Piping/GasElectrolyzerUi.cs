// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;
using GasMixEntry = Content.Shared.Atmos.Components.GasAnalyzerComponent.GasMixEntry;

namespace Content.Shared.DeadSpace.Atmos.Piping;

[Serializable, NetSerializable]
public enum GasElectrolyzerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GasElectrolyzerToggleMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class GasElectrolyzerBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Enabled;
    public bool Powered;
    public GasMixEntry Input;
    public GasMixEntry Output;

    public GasElectrolyzerBoundUserInterfaceState(bool enabled, bool powered, GasMixEntry input, GasMixEntry output)
    {
        Enabled = enabled;
        Powered = powered;
        Input = input;
        Output = output;
    }
}
