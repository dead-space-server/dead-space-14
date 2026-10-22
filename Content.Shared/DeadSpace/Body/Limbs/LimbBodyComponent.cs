// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbBodyComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<LimbZoneState> Zones = LimbZoneState.CreateAll();

    [DataField, AutoNetworkedField]
    public List<LimbOrganState> Organs = LimbOrganRules.CreateAll();

    [DataField]
    public LimbOrganMask Present = LimbOrganMask.Standard;

    [DataField]
    public float Wound = 1f;

    [DataField]
    public float LiquidSever;

    [DataField, AutoNetworkedField]
    public float Mouth;

    [DataField, AutoNetworkedField]
    public byte EyesGone;

    [DataField, AutoNetworkedField]
    public float EyeBurn;

    [DataField, AutoNetworkedField]
    public bool LungWhistle;

    public DamageSpecifier? PendingRefund;

    public LimbZone? LedgerZone;

    public LimbZone? ArmorZone;

    public LimbZone? FireOrigin;

    public TimeSpan FireOriginTime;

    public bool StrikeOpen;

    public bool StaminaArmed;

    public float StaminaScale = 1f;

    public bool AllowDrop = true;

    public bool DropHeavyOnly;

    public int LedgerSilence;

    [DataField]
    public TimeSpan? Died;

    public TimeSpan PelletUntil;

    public int PelletHits;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbZone? Selected;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbAimComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Aiming;

    [DataField, AutoNetworkedField]
    public LimbZone CalledZone = LimbZone.Torso;
}

[ByRefEvent]
public readonly record struct LimbSeveredEvent(LimbZone Zone);

[ByRefEvent]
public readonly record struct LimbConcussionEvent(bool Heavy);

[ByRefEvent]
public readonly record struct LimbBrainRuptureEvent;

[RegisterComponent]
public sealed partial class LimbActiveComponent : Component
{
    public const float Period = 1f;
    public const float CryoPeriod = 5f;

    public float Accumulator;
    public float CryoAccumulator;
}

[RegisterComponent]
public sealed partial class LimbWhistleComponent : Component
{
    public const float Period = 8f;

    public float Accumulator;
}
