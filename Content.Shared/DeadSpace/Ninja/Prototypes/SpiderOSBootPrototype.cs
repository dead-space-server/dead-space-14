using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Prototypes;

[Serializable, NetSerializable]
public enum SpiderOSBootCheck : byte
{
    SelfTest = 0,

    Authorized = 1,

    NotActivated = 2,

    ModulesReady = 3,

    VisorSecure = 4,

    HelmetSecure = 5,

    MaskSecure = 6,

    GlovesSecure = 7,

    BootsSecure = 8,

    SuitFasten = 9,

    BatteryReport = 10,
}

[DataRecord]
public partial record struct SpiderOSBootStep()
{
    [DataField]
    public float Delay = 1f;

    [DataField]
    public LocId Log = string.Empty;

    [DataField]
    public SpiderOSBootCheck? Check;
}

[Prototype("SpiderOSBoot")]
public sealed partial class SpiderOSBootPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<SpiderOSBootStep> Steps = new();
}