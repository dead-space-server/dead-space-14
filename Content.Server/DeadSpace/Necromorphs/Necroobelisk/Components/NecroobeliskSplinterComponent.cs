// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;

[RegisterComponent]
public sealed partial class NecroobeliskSplinterComponent : Component
{
    [DataField]
    public bool SpawnsBlackObelisk;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUtilAddCharge = TimeSpan.FromSeconds(120);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan AddChargeTime = TimeSpan.Zero;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int SanityDamageRepeatingTime = 5;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float Range = 5f;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float SanityDamage = 20f;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public int SanityDamageImpulseCount = 8;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public float Duration = 8f;

    [DataField]
    public float InteractionDuration = 5f;

    [DataField]
    public float StimulatedInteractionDuration = 2.8f;

    [DataField]
    public float SpeedModifier = 0.3f;

    [DataField]
    public float ScreenshakeTrauma = 1f;

    [DataField]
    public float ScreenshakeFrequency = 0.04f;

    [DataField("energyConsumption"), ViewVariables(VVAccess.ReadWrite)]
    public float EnergyConsumption = 50000f;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public SoundSpecifier? SoundHeadaches = default;
}

