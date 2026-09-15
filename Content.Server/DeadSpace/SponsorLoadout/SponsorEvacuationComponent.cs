// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Network;

namespace Content.Server.DeadSpace.SponsorLoadout;

[RegisterComponent, Access(typeof(SponsorEvacuationSystem))]
public sealed partial class SponsorEvacuationComponent : Component
{
    public NetUserId UserId;
    public EntityUid? Implant;
}
