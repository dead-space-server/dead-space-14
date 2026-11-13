// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Dataset;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public enum RuneEffectTarget : byte
{
    Caster,
    Target,
    AroundCaster,
    AroundPoint,
    Auto,
}

public enum RuneSpawnPattern : byte
{
    Auto,
    AtPoint,
    AtCaster,
    LineInFront,
    LineAtPoint,
    RingAroundCaster,
    AreaAtPoint,
    RingAtPoint,
}

public enum RuneTeleportMode : byte
{
    CasterToPoint,
    SwapWithTarget,
    ScatterTargets,
    ScatterCaster,
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class RuneSpellEffect
{
}

public abstract partial class RuneTargetedEffect : RuneSpellEffect
{
    [DataField]
    public RuneEffectTarget Target = RuneEffectTarget.Target;

    [DataField]
    public float Radius = 2f;

    [DataField]
    public bool IncludeCaster;

    [DataField]
    public bool OnlyDead;
}

public sealed partial class RuneDecayEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Radius = 2f;

    [DataField]
    public bool AroundCaster;
}

public sealed partial class RuneBarrageEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Count = 6;

    [DataField]
    public float Radius = 5f;

    [DataField]
    public float Interval = 0.4f;
}

public sealed partial class RuneReviveEffect : RuneTargetedEffect
{
    [DataField]
    public float Immortality;
}

public sealed partial class RuneTimeStopEffect : RuneSpellEffect
{
    [DataField]
    public float Radius = 5f;

    [DataField]
    public float Duration = 30f;
}

public sealed partial class RunePortalEffect : RuneSpellEffect
{
    [DataField]
    public EntProtoId Prototype = "RuneWizardPortal";
}

public sealed partial class RuneArenaEffect : RuneSpellEffect
{
    [DataField]
    public EntProtoId Challenger = "RuneArenaChallenger";

    [DataField]
    public int Challengers = 3;

    [DataField]
    public float Countdown = 20f;

    [DataField]
    public float Duration = 180f;

    [DataField]
    public float SpectatorRange = 10f;
}

public sealed partial class RuneProjectileEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float Speed = 20f;

    [DataField]
    public int Count = 1;

    [DataField]
    public float Spread = 12f;
}

public sealed partial class RuneSpawnEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public RuneSpawnPattern Pattern = RuneSpawnPattern.AtPoint;

    [DataField]
    public int Size = 1;

    [DataField]
    public float? Lifetime;

    [DataField]
    public bool SkipBlocked = true;

    [DataField]
    public bool PassableByCaster;
}

public sealed partial class RuneSpawnRandomEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();

    [DataField]
    public RuneSpawnPattern Pattern = RuneSpawnPattern.AreaAtPoint;

    [DataField]
    public int Size = 1;

    [DataField]
    public int Count = 3;

    [DataField]
    public float? Lifetime;

    [DataField]
    public bool NoGhostRole;
}

public sealed partial class RuneDamageEffect : RuneTargetedEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public bool IgnoreResistances;

    [DataField]
    public bool MendInternal;
}

public sealed partial class RuneStaminaEffect : RuneTargetedEffect
{
    [DataField]
    public float Amount = 30f;
}

public sealed partial class RuneKnockdownEffect : RuneTargetedEffect
{
    [DataField]
    public float Time = 2f;
}

public sealed partial class RuneSlowEffect : RuneTargetedEffect
{
    [DataField]
    public float Time = 4f;

    [DataField]
    public float Multiplier = 0.5f;
}

public sealed partial class RuneIgniteEffect : RuneTargetedEffect
{
    [DataField]
    public float FireStacks = 2f;
}

public sealed partial class RuneExtinguishEffect : RuneTargetedEffect
{
}

public sealed partial class RunePushEffect : RuneSpellEffect
{
    [DataField]
    public float Speed = 10f;

    [DataField]
    public float Radius = 4f;

    [DataField]
    public bool AtPoint;

    [DataField]
    public bool Auto;
}

public sealed partial class RuneTeleportEffect : RuneSpellEffect
{
    [DataField]
    public RuneTeleportMode Mode = RuneTeleportMode.CasterToPoint;

    [DataField]
    public float Radius = 6f;

    [DataField]
    public float Reach = 2.5f;
}

public sealed partial class RunePolymorphEffect : RuneTargetedEffect
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Polymorph;

    [DataField]
    public ProtoId<PolymorphPrototype>? OnCaster;
}

public sealed partial class RuneComponentsEffect : RuneTargetedEffect
{
    [DataField]
    public EntProtoId? Template;

    [DataField]
    public EntProtoId? FromAction;

    [DataField]
    public float? Duration;
}

public sealed partial class RuneParalyzeEffect : RuneTargetedEffect
{
    [DataField]
    public float Time = 3f;
}

public sealed partial class RuneRecallEffect : RuneSpellEffect
{
}

public sealed partial class RuneOrbitShieldEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Count = 3;

    [DataField]
    public int MaxShields = 8;

    [DataField]
    public float Duration = 60f;

    [DataField]
    public float MinDamage = 5f;

    [DataField]
    public float MinStamina = 15f;
}

public sealed partial class RuneRegenerationEffect : RuneTargetedEffect
{
    [DataField(required: true)]
    public DamageSpecifier PerSecond = new();

    [DataField]
    public float Duration = 20f;
}

public sealed partial class RuneStatusEffect : RuneTargetedEffect
{
    [DataField(required: true)]
    public EntProtoId Status;

    [DataField]
    public float Time = 30f;
}

public sealed partial class RuneTelekinesisEffect : RuneSpellEffect
{
    [DataField]
    public float PullSpeed = 12f;

    [DataField]
    public float HurlSpeed = 20f;

    [DataField]
    public float GrabRadius = 0.8f;
}

public sealed partial class RuneChargeEffect : RuneSpellEffect
{
    [DataField]
    public int Charges = 1;
}

public sealed partial class RuneMindSwapEffect : RuneSpellEffect
{
    [DataField]
    public float Stun = 10f;
}

public sealed partial class RuneAnimateEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public EntProtoId Template;

    [DataField]
    public int Count = 1;

    [DataField]
    public float Radius = 1.5f;
}

public sealed partial class RuneKnockEffect : RuneSpellEffect
{
    [DataField]
    public float Range = 6f;
}

public sealed partial class RuneGibEffect : RuneTargetedEffect
{
}

public sealed partial class RuneLightningEffect : RuneSpellEffect
{
    [DataField]
    public EntProtoId Prototype = "WizardLightning";

    [DataField]
    public float Range = 5f;

    [DataField]
    public int Bolts = 2;
}

public sealed partial class RuneEmpEffect : RuneSpellEffect
{
    [DataField]
    public float Range = 3f;

    [DataField]
    public float Energy = 25000f;

    [DataField]
    public float Duration = 10f;

    [DataField]
    public bool AtPoint;

    [DataField]
    public bool Auto;
}

public sealed partial class RuneSpeakEffect : RuneTargetedEffect
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Lines;
}

public sealed partial class RuneRandomEffect : RuneSpellEffect
{
    [DataField(required: true)]
    public List<RuneSpellEffect> Options = new();
}
