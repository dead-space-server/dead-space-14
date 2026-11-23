// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RoundEnd.Credits;

namespace Content.Server.DeadSpace.RoundEnd.Credits;

[ByRefEvent]
public record struct RoundEndCreditsTeamResultEvent(RoundEndCreditsTeamResult? Result);
