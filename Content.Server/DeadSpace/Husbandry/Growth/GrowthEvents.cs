// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Husbandry.Growth;

/// <summary>
/// Raised on an animal when it enters a growth stage, including the first one.
/// </summary>
[ByRefEvent]
public readonly record struct GrowthStageChangedEvent(int Index, GrowthStageDef Stage);
