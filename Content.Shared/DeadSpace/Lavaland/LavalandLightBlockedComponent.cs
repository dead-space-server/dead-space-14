// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The deep night will not let this light (a PDA or helmet light on cooldown) or this night vision turn on.
/// Networked, so the toggle itself refuses - also on the client, without a flash of light before the server
/// turns it off again.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandLightBlockedComponent : Component;
