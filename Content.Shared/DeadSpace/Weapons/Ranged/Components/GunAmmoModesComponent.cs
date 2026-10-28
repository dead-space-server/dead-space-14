// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Components;

/// <summary>
/// A gun with several fire modes, switched with its own action. A mode either fires the gun's own
/// self-recharging shots (<see cref="GunAmmoMode.Proto"/> - the bolt its BasicEntityAmmoProvider makes) or takes its
/// ammo from one of the gun's item slots (<see cref="GunAmmoMode.Slot"/>): a magazine, a single shell or grenade loaded
/// into it, like an underbarrel launcher.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(GunAmmoModesSystem))]
public sealed partial class GunAmmoModesComponent : Component
{
    [DataField(required: true)]
    public List<GunAmmoMode> Modes = new();

    [DataField, AutoNetworkedField]
    public int Selected;

    /// <summary>The action, given while the gun is held, that switches to the next mode.</summary>
    [DataField]
    public EntProtoId? SwitchAction = "ActionGunAmmoModeSwitch";

    [DataField, AutoNetworkedField]
    public EntityUid? SwitchActionEntity;

    /// <summary>Using the gun in hand switches the mode too (off where that key does something else, a bolt).</summary>
    [DataField]
    public bool SwitchOnUse = true;

    [DataField]
    public SoundSpecifier? SwitchSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Misc/selector.ogg");

    public GunAmmoMode? Current => Selected >= 0 && Selected < Modes.Count ? Modes[Selected] : null;

    /// <summary>The current mode takes its ammo from a slot: the gun's own ammo provider stays out of it.</summary>
    public bool SlotSelected => Current?.Slot != null;
}

[DataDefinition]
public sealed partial class GunAmmoMode
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>What the gun's BasicEntityAmmoProvider fires in this mode; null - whatever it was set to.</summary>
    [DataField]
    public EntProtoId? Proto;

    /// <summary>
    /// The item slot this mode takes its ammo from (a magazine or a single cartridge in it). Null - the gun's own
    /// ammo provider.
    /// </summary>
    [DataField]
    public string? Slot;

    /// <summary>
    /// How many of the gun's own charges this mode holds (the repeater's two, the shotgun's one); null - as it is.
    /// Switched to a smaller one, the charges over it are lost; to a larger one, the rest recharge.
    /// </summary>
    [DataField]
    public int? Capacity;

    /// <summary>Charges of the gun's own ammo spent on a shot; the mode does not fire with fewer left.</summary>
    [DataField]
    public int Cost = 1;

    [DataField]
    public float FireRateMultiplier = 1f;

    /// <summary>The self-recharging ammo comes back this many times slower while this mode is on.</summary>
    [DataField]
    public float RechargeMultiplier = 1f;

    [DataField]
    public SoundSpecifier? SoundGunshot;
}
