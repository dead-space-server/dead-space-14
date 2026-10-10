using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Sectants;

/// <summary>Marks entity as disappearing on death without leaving a corpse, broadcasting a message.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectantDisappearComponent : Component
{
    [DataField, AutoNetworkedField] public LocId Message = "sectant-disappear-message";
    [DataField, AutoNetworkedField] public float Radius = 30f;
    [DataField, AutoNetworkedField] public bool DeleteEntity = true;
    [DataField, AutoNetworkedField] public SoundSpecifier? Sound;
    [DataField, AutoNetworkedField] public EntProtoId? EffectPrototype = "SectantDisappearEffect";
}

/// <summary>Teleports the entity to a random nearby tile whenever it takes damage.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectantRandomTeleportOnDamageComponent : Component
{
    [DataField, AutoNetworkedField] public float Radius = 8f;
    [DataField, AutoNetworkedField] public float MinDistance = 2f;
    [DataField, AutoNetworkedField] public float Cooldown = 2f;
    [DataField, AutoNetworkedField] public SoundSpecifier? Sound;
    [DataField, AutoNetworkedField] public EntProtoId? EffectPrototype = "SectantBlinkEffect";

    /// <summary>Server-side cooldown timestamp.</summary>
    [ViewVariables] public TimeSpan LastTeleport;
}

/// <summary>Remembers the original visibility for toggling invisibility on/off.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectantInvisibilityToggleComponent : Component
{
    [DataField, AutoNetworkedField] public float HiddenVisibility = 0f;
    [DataField, AutoNetworkedField] public float VisibleVisibility = 1f;
    [DataField, AutoNetworkedField] public bool IsHidden;
}

/// <summary>Marks hacked airlocks / access readers to revert after a delay.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SectantHackedComponent : Component
{
    [DataField] public TimeSpan RevertAt;
    [DataField] public bool WasBolted;
    [DataField] public bool WasOpen;
}

/// <summary>Ticks status effects on nearby enemies (sleep, pacify, blind).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectantHallucinationAuraComponent : Component
{
    [DataField, AutoNetworkedField] public float Radius = 4f;
    [DataField, AutoNetworkedField] public float TickInterval = 1f;
    [DataField, AutoNetworkedField] public EntProtoId SleepEffect = "StatusEffectForcedSleeping";
    [DataField, AutoNetworkedField] public EntProtoId PacifyEffect = "StatusEffectPacified";
    [DataField, AutoNetworkedField] public EntProtoId BlindEffect = "StatusEffectTemporaryBlindness";
    [ViewVariables] public TimeSpan NextTick;
}

/// <summary>Aura visual overlay rendered on the client.</summary>`
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectantAuraOverlayComponent : Component
{
    [DataField("color"), AutoNetworkedField]
    public Color Color { get; set; } = Color.Purple;

    [DataField("shader"), AutoNetworkedField]
    public string Shader { get; set; } = "SectantAuraShader";

    [DataField("radius"), AutoNetworkedField]
    public float Radius { get; set; } = 4f;

    [DataField("pulseSpeed"), AutoNetworkedField]
    public float PulseSpeed { get; set; } = 2f;

    [DataField("outlineWidth"), AutoNetworkedField]
    public float OutlineWidth { get; set; } = 0.012f;

    [DataField("spikeCount"), AutoNetworkedField]
    public float SpikeCount { get; set; } = 7f;
}
