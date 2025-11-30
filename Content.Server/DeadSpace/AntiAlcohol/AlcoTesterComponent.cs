// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.AntiAlcohol;

[RegisterComponent]
public sealed partial class AlcoTesterComponent : Component
{
    [DataField] public float ScanDelaySec = 2.0f;
}
