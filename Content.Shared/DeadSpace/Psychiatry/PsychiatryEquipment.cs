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
public sealed partial class LobotomyToolComponent : Component
{
    [DataField]
    public int Steps = 3;

    [DataField]
    public float StepSeconds = 4f;

    [DataField]
    public float ComplicationChance = 0.3f;

    public int Progress;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ShockTherapyComponent : Component
{
    [DataField]
    public float SideEffectChance = 0.18f;

    [DataField]
    public float ShockDamage = 40f;

    [DataField]
    public float DoAfterMinSeconds = 40f;

    [DataField]
    public float DoAfterMaxSeconds = 60f;

    public int Progress;

    public int ProgressSteps = 1;

    public float ProgressHit;

    public bool ProgressLiving;
}

[Serializable, NetSerializable]
public sealed partial class LobotomyDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int StepIndex;

    public LobotomyDoAfterEvent(int stepIndex)
    {
        StepIndex = stepIndex;
    }

    public LobotomyDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new LobotomyDoAfterEvent(StepIndex);
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
    public float Delay = 12f;

    [DataField]
    public float FaultChance = 0.3f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class IonScrubberComponent : Component
{
    [DataField]
    public float Delay = 8f;

    [DataField]
    public float FaultChance = 0.4f;

    [DataField]
    public float ShockDamage = 15f;
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
public sealed partial class HardResetDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new HardResetDoAfterEvent();
}

[Serializable, NetSerializable]
public sealed partial class IonScrubDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new IonScrubDoAfterEvent();
}

[Serializable, NetSerializable]
public sealed partial class CascadeSpikeDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new CascadeSpikeDoAfterEvent();
}
