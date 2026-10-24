// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Microscope;

[Serializable, NetSerializable]
public sealed class MicroscopeActionMessage : BoundUserInterfaceMessage
{
    public MicroscopeUiAction Action;

    public MicroscopeActionMessage(MicroscopeUiAction action)
    {
        Action = action;
    }
}
