// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Network;

namespace Content.Server.DeadSpace.NewLife;

[RegisterComponent, Access(typeof(NewLifeSystem))]
public sealed partial class NewLifeTrackedBodyComponent : Component
{
    public NetUserId UserId;
}
