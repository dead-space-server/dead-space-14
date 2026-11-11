// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

public enum LimbRestore : byte
{
    Bleed,
    Fracture,
    Shard,
    Flesh,
    Skin,
    Atrophy,
    Health,
    Organs,
    Infection,
    Rejection,
    Limbs,
    MissingOrgans,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbRestorerComponent : Component
{
    [DataField]
    public float Prime = 4f;

    [DataField]
    public float PrimeWindow = 60f;

    [DataField]
    public float Delay = 8f;

    [DataField]
    public float SelfDelay = 12f;

    [DataField]
    public bool AllowSelf = true;

    [DataField, AutoNetworkedField]
    public int Charges = 1;

    [DataField]
    public bool DeleteEmpty = true;

    [DataField]
    public List<LimbRestore> Heal = new();

    [DataField]
    public float Health = 0.6f;

    [DataField]
    public float Regrow = 0.3f;

    [DataField]
    public DamageSpecifier? Cost;

    [DataField]
    public SoundSpecifier? PrimeSound;

    [DataField]
    public SoundSpecifier? UseSound;

    [DataField]
    public LocId PrimedLine = "limb-restorer-primed";

    [DataField]
    public LocId DoneLine = "limb-restorer-done";

    [DataField, AutoNetworkedField]
    public TimeSpan PrimedUntil;

    [DataField]
    public bool AutoCrit;
}

public sealed partial class LimbRestorerImplantEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class LimbRestorerPrimeDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class LimbRestorerApplyDoAfterEvent : SimpleDoAfterEvent;
