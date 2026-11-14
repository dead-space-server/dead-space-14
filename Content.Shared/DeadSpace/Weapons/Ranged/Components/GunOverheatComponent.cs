// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Components;

/// <summary>
/// A self-recharging gun that heats up when fired too often: every shot adds heat, which cools off with time. Past
/// <see cref="Threshold"/> each charge takes longer to come back, the hotter the longer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GunOverheatSystem))]
public sealed partial class GunOverheatComponent : Component
{
    [DataField]
    public float HeatPerShot = 1f;

    /// <summary>Heat lost per second.</summary>
    [DataField]
    public float Cooling = 0.6f;

    /// <summary>Up to this much heat the recharge is as usual.</summary>
    [DataField]
    public float Threshold = 3f;

    /// <summary>Each point of heat over the threshold makes the recharge this much longer (0.5 - half as long again).</summary>
    [DataField]
    public float SlowdownPerHeat = 0.6f;

    /// <summary>The recharge is never longer than this many times its usual length.</summary>
    [DataField]
    public float MaxSlowdown = 4f;

    /// <summary>Shown to the shooter as the barrel goes over the threshold.</summary>
    [DataField]
    public LocId? OverheatPopup = "gun-overheat-popup";

    [DataField]
    public LocId? OverheatExamine = "gun-overheat-examine";

    /// <summary>Heat as of <see cref="HeatTime"/>; it has been cooling off since.</summary>
    [DataField, AutoNetworkedField]
    public float Heat;

    [DataField, AutoNetworkedField]
    public TimeSpan HeatTime;
}
