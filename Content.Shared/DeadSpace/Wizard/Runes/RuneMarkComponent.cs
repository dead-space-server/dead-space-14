// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[Serializable, NetSerializable]
public enum RuneMark : byte
{
    None,
    Burning,
    Wet,
    Dirty,
    Frozen,
    Poisoned,
    Bleeding,
    Withering,
    Displaced,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public RuneMark Mark;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan End;
}

[Prototype]
public sealed partial class RuneMarkIconPrototype : StatusIconPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<RuneMarkIconPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
}

public readonly record struct RuneMarkResult(float Scale, RuneMark Next, bool Extinguish);

public static class RuneMarkRules
{
    public const float Strong = 2f;
    public const float Weak = 0.75f;
    public const float Duration = 30f;

    public const string Fire = "RuneElementFire";
    public const string Water = "RuneElementWater";
    public const string Earth = "RuneElementEarth";
    public const string Lightning = "RuneElementLightning";
    public const string Ice = "RuneElementIce";
    public const string Air = "RuneElementAir";
    public const string Poison = "RuneElementPoison";
    public const string Blood = "RuneElementBlood";
    public const string Death = "RuneElementDeath";
    public const string Life = "RuneElementLife";
    public const string Void = "RuneElementVoid";
    public const string Chaos = "RuneElementChaos";

    public static readonly RuneMark[] ChaosMarks =
    {
        RuneMark.Burning,
        RuneMark.Wet,
        RuneMark.Dirty,
        RuneMark.Frozen,
    };

    public static RuneMarkResult Resolve(string element, RuneMark current, bool lying, int chaosRoll)
    {
        switch (element)
        {
            case Fire:
                return current switch
                {
                    RuneMark.Wet => new RuneMarkResult(Weak, RuneMark.Burning, false),
                    RuneMark.Frozen or RuneMark.Poisoned => new RuneMarkResult(Strong, RuneMark.Burning, false),
                    _ => new RuneMarkResult(1f, RuneMark.Burning, false),
                };
            case Water:
                return current switch
                {
                    RuneMark.Burning => new RuneMarkResult(Weak, RuneMark.Wet, true),
                    RuneMark.Bleeding => new RuneMarkResult(Weak, RuneMark.Wet, false),
                    _ => new RuneMarkResult(1f, RuneMark.Wet, false),
                };
            case Earth:
                return new RuneMarkResult(lying ? Strong : 1f, RuneMark.Dirty, current == RuneMark.Burning);
            case Lightning:
                return current switch
                {
                    RuneMark.Wet => new RuneMarkResult(Strong, current, false),
                    RuneMark.Dirty => new RuneMarkResult(Weak, current, false),
                    _ => new RuneMarkResult(1f, current, false),
                };
            case Ice:
                return current switch
                {
                    RuneMark.Wet => new RuneMarkResult(Strong, RuneMark.Frozen, false),
                    RuneMark.Burning => new RuneMarkResult(1f, RuneMark.Frozen, true),
                    _ => new RuneMarkResult(1f, RuneMark.Frozen, false),
                };
            case Air:
                return current switch
                {
                    RuneMark.Wet or RuneMark.Dirty or RuneMark.Frozen => new RuneMarkResult(1f, RuneMark.None, false),
                    RuneMark.Burning => new RuneMarkResult(Strong, RuneMark.Burning, false),
                    _ => new RuneMarkResult(1f, current, false),
                };
            case Poison:
                return current switch
                {
                    RuneMark.Bleeding => new RuneMarkResult(Strong, RuneMark.Poisoned, false),
                    RuneMark.Wet => new RuneMarkResult(Weak, RuneMark.Poisoned, false),
                    _ => new RuneMarkResult(1f, RuneMark.Poisoned, false),
                };
            case Blood:
                return new RuneMarkResult(current == RuneMark.Bleeding ? Strong : 1f, RuneMark.Bleeding, false);
            case Death:
                return new RuneMarkResult(current is RuneMark.Poisoned or RuneMark.Bleeding ? Strong : 1f, RuneMark.Withering, false);
            case Life:
                return new RuneMarkResult(1f, current is RuneMark.Poisoned or RuneMark.Withering or RuneMark.Bleeding ? RuneMark.None : current, false);
            case Void:
                return new RuneMarkResult(1f, RuneMark.Displaced, false);
            case Chaos:
                var next = ChaosMarks[(chaosRoll % ChaosMarks.Length + ChaosMarks.Length) % ChaosMarks.Length];
                return new RuneMarkResult(current == RuneMark.None ? 1f : Strong, next, false);
        }

        return new RuneMarkResult(1f, current, false);
    }
}
