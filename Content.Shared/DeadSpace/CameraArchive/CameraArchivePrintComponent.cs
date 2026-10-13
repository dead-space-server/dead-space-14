// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.CameraArchives;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CameraArchivePrintComponent : Component
{
    [AutoNetworkedField]
    public byte[] Image = [];
}
