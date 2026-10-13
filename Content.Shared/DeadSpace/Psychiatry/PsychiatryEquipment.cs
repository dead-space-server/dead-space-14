// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Psychiatry;

[RegisterComponent]
public sealed partial class PsychogenFilterComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroMeshComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalGagComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class EncephalographComponent : Component
{
    [ViewVariables]
    public EntityUid? ScanTarget;
}

[RegisterComponent]
public sealed partial class EncephalographSubjectComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Scanners = new();

    [ViewVariables]
    public Dictionary<PsychiatryBrainRegion, float> Activity = new();

    [ViewVariables]
    public TimeSpan NextUiPush;

    [ViewVariables]
    public TimeSpan TypingUntil;
}

[Serializable, NetSerializable]
public enum EncephalographUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class EncephalographBoundUserInterfaceState : BoundUserInterfaceState
{
    public NetEntity Target;
    public Dictionary<PsychiatryBrainRegion, float> Activity;
    public bool Positronic;

    public EncephalographBoundUserInterfaceState(NetEntity target, Dictionary<PsychiatryBrainRegion, float> activity, bool positronic)
    {
        Target = target;
        Activity = activity;
        Positronic = positronic;
    }
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShockTherapyComponent : Component
{
    [DataField]
    public float SideEffectChance = 0.25f;

    [DataField]
    public float ShockDamage = 16f;

    [DataField]
    public float DoAfterMinSeconds = 40f;

    [DataField]
    public float DoAfterMaxSeconds = 60f;

    public int Progress;

    public int ProgressSteps = 1;

    public float ProgressHit;

    public bool ProgressLiving;

    public TimeSpan ShockUntil;
}

[Serializable, NetSerializable]
public enum ShockTherapyVisuals : byte
{
    Shocking,
}

[Serializable, NetSerializable]
public sealed partial class ShockTherapyDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Step;

    [DataField]
    public int Steps = 1;

    [DataField]
    public float StepSeconds = 2.5f;

    [DataField]
    public float HitDamage;

    [DataField]
    public bool Living;

    public override DoAfterEvent Clone()
    {
        return new ShockTherapyDoAfterEvent
        {
            Step = Step,
            Steps = Steps,
            StepSeconds = StepSeconds,
            HitDamage = HitDamage,
            Living = Living,
        };
    }
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FirmwarePatchComponent : Component
{
    [DataField]
    public float Delay = 6f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HardResetProbeComponent : Component
{
    [DataField]
    public float ShockDamage = 16f;

    [DataField]
    public float DoAfterMinSeconds = 40f;

    [DataField]
    public float DoAfterMaxSeconds = 60f;

    public int Progress;

    public int ProgressSteps = 4;

    public float ProgressHit;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AdminCurePatchComponent : Component
{
    [DataField]
    public float Delay = 3f;
}

[Serializable, NetSerializable]
public sealed partial class AdminCurePatchDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new AdminCurePatchDoAfterEvent();
}
[RegisterComponent, NetworkedComponent]
public sealed partial class CascadeSpikeComponent : Component
{
    [DataField]
    public float Delay = 4f;
}

[Serializable, NetSerializable]
public sealed partial class FirmwarePatchDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new FirmwarePatchDoAfterEvent();
}

[Serializable, NetSerializable]
public sealed partial class HardResetDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Step;

    [DataField]
    public int Steps = 4;

    [DataField]
    public float StepSeconds = 10f;

    [DataField]
    public float HitDamage;

    public override DoAfterEvent Clone()
    {
        return new HardResetDoAfterEvent
        {
            Step = Step,
            Steps = Steps,
            StepSeconds = StepSeconds,
            HitDamage = HitDamage,
        };
    }
}

[Serializable, NetSerializable]
public sealed partial class CascadeSpikeDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new CascadeSpikeDoAfterEvent();
}
