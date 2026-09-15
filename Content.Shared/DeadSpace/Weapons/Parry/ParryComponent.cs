// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Whitelist;
using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Weapons.Parry;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ParryComponent : Component
{
    /// <summary>
    /// Attacking melee weapons which this weapon can parry. Supports prototype, tag, component and size filters.
    /// </summary>
    [DataField]
    public EntityWhitelist ParryableWeapons = new();

    [DataField]
    public TimeSpan SuccessCooldown = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan FailureCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan ParryWindow = TimeSpan.FromSeconds(1);

    [DataField]
    public float KnockbackDistance = 1f;

    [DataField]
    public float KnockbackSpeed = 5f;

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Weapons/block_metal1.ogg");

    [DataField]
    public float RarePopupChance = 0.03f;

    [DataField]
    public ProtoId<AlertPrototype> CooldownAlert = "ParryCooldown";

    /// <summary>
    /// Whether the item must be toggled on before it can parry.
    /// </summary>
    [DataField]
    public bool RequiresActiveToggle;

    [AutoNetworkedField]
    public TimeSpan CooldownStart;

    [AutoNetworkedField]
    public TimeSpan NextParry;

    [AutoNetworkedField]
    public TimeSpan ActiveUntil;
}
