// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Construction;

/// <summary>
/// Marks an entity that a player built through the construction menu.
/// The marker follows the entity through later construction graph steps.
/// </summary>
[RegisterComponent]
public sealed partial class PlayerConstructedComponent : Component;
