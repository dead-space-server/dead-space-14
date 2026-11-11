using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandSpectralBladeComponent : Component
{
    [DataField]
    public string DamageType = "Slash";

    [DataField]
    public FixedPoint2 BaseDamage = FixedPoint2.New(6);

    [DataField]
    public FixedPoint2 DamagePerSpirit = FixedPoint2.New(4);

    [DataField]
    public FixedPoint2 MaxBonusDamage = FixedPoint2.New(40);

    [DataField]
    public int MaxSpirits = 10;

    [DataField]
    public float NearbyGhostRange = 0f;

    [DataField]
    public float CorpseRange = 8f;

    [DataField]
    public float ActivationDuration = 45f;

    [DataField]
    public float ActivationCooldown = 60f;

    [DataField]
    public float FailedActivationCooldown = 5f;

    [DataField]
    public float SummonResponseTime = 20f;

    [DataField]
    public float GhostSummonCooldown = 180f;

    [DataField]
    public int MaxSummonWindows = 20;

    [DataField]
    public SoundSpecifier ActivationSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/SpectralBlade/ghost2.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.05f),
    };

    [ViewVariables]
    public int CurrentSpirits;

    [ViewVariables]
    public TimeSpan ActiveUntil;

    [ViewVariables]
    public TimeSpan NextActivation;

    /// <summary>The ghosts following it shown to the living while it is active.</summary>
    [ViewVariables]
    public HashSet<EntityUid> RevealedGhosts = new();
}
