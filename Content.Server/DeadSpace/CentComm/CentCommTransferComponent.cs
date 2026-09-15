// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.CentComm;

[RegisterComponent]
public sealed partial class CentCommTransferComponent : Component
{
    public EntityCoordinates Origin;
    public Angle Rotation;
    public TimeSpan StartAt;
    public bool JumpStarted;
    public bool Arrived;
    public string Parallax = default!;
    public string? Weather;
    public float? Temperature;
    public ICommonSession? Requester;
    public readonly List<(EntityUid Dock, EntityUid OtherDock)> Docks = new();
}
