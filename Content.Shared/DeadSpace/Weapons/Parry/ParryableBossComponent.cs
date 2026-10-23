// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Parry;

/// <summary>
/// Marks a lavaland boss: its blows (and the rushes and saws that are not melee attacks) can be parried by weapons
/// whose <see cref="ParryComponent.ParryableWeapons"/> list this component.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ParryableBossComponent : Component;
