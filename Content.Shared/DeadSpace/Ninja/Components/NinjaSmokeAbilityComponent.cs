using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSmokeAbilityComponent : Component
{
    [DataField]
    public EntProtoId ActionSmoke = "NinjaSmokeAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionSmokeEntity;

    [DataField]
    public EntProtoId ActionAutoSmoke = "ToggleNinjaAutoSmokeAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionAutoSmokeEntity;

    /// <summary>
    /// How long the smoke stays for, after it has spread (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How much the smoke will spread.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public int SpreadAmount;

    /// <summary>
    /// Smoke entity to spawn.
    /// Defaults to smoke but you can use foam if you want.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId SmokePrototype = "Smoke";

    /// <summary>
    /// How long the smoke stays in auto mode for, after it has spread (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DurationAutoMode = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How much the smoke will spread in auto mode.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public int SpreadAmountAutoMode;


    /// <summary>
    /// Will the ability be activated by others?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoMode = false;

    /// <summary>
    /// The <see cref="SoundSpecifier"/> to play.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SmokeSound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg");

    [DataField("energyCost")]
    public float EnergyCost = 50f;

    [DataField("energyCostAutoMode")]
    public float EnergyCostAutoMode = 25f;
}

public sealed partial class NinjaSmokeAbilityActionEvent : InstantActionEvent;

public sealed partial class NinjaToggleAutoSmokeActionEvent : InstantActionEvent;