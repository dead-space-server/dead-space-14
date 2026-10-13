// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.CameraArchives;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.CameraArchives;

[RegisterComponent, NetworkedComponent]
public sealed partial class CameraArchiveConsoleComponent : Component
{
    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/tray_eject.ogg");

    [DataField]
    public ProtoId<Content.Shared.DeadSpace.Photocopier.PaperworkFormPrototype> Form = "CameraArchive";

    public TimeSpan NextPrint;
}
