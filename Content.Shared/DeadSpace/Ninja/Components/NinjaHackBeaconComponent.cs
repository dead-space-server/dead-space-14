using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class NinjaHackBeaconComponent : Component
{
    [DataField]
    public ProtoId<CurrencyPrototype> SpiderCoinCurrencyPrototype = "NinjaHackCurrency";

    [DataField]
    public TimeSpan MineDelay = TimeSpan.FromSeconds(25f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextMineTime = TimeSpan.Zero;

    [DataField]
    public int MineAtOnce = 10_000;

    [DataField]
    public int ConversionRate = 1_000;

    [DataField]
    public bool Active;

    [DataField]
    public EntityUid? Mind;
}