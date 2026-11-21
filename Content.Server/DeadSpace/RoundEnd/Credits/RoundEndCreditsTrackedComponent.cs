// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.RoundEnd.Credits;

[RegisterComponent, Access(typeof(RoundEndCreditsStatsSystem))]
public sealed partial class RoundEndCreditsTrackedComponent : Component
{
    public EntityUid? LastAttackerMind;

    public TimeSpan LastAttackTime;

    public bool Cuffed;
}
