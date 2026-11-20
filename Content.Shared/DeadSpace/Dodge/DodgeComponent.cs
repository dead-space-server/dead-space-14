// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Dodge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DodgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PassiveChance = 0.15f;

    [DataField, AutoNetworkedField]
    public float ActiveChance = 0.35f;

    [DataField, AutoNetworkedField]
    public float GuaranteedStaminaCost = 6f;

    [DataField, AutoNetworkedField]
    public bool StaminaDodgeMode;

    [DataField]
    public LocId? NoStaminaPopup;

    [DataField]
    public float NoStaminaPopupCooldown = 1f;

    public TimeSpan NextStaminaPopup;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DodgeVisualComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color StartColor = Color.White;

    [DataField, AutoNetworkedField]
    public Color EndColor = Color.Transparent;

    [DataField, AutoNetworkedField]
    public float Lifetime = 0.32f;

    [DataField, AutoNetworkedField]
    public string FallbackEffect = "MantisDodgeEffect";

    [DataField, AutoNetworkedField]
    public float SidewaysOffset = 0.16f;

    [DataField, AutoNetworkedField]
    public float BackwardOffset = 0.12f;

    [DataField, AutoNetworkedField]
    public float OuterBackwardOffset = 0.08f;

    [DataField]
    public float JitterDuration = 0.18f;

    [DataField]
    public float JitterAmplitude = 4f;

    [DataField]
    public float JitterFrequency = 24f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DodgeModeImplantComponent : Component
{
    [DataField]
    public EntProtoId ModeAction = "";

    [DataField, AutoNetworkedField]
    public EntityUid? ModeActionEntity;

    [DataField]
    public ResPath ActionRsi = new();

    [DataField]
    public string ChanceIcon = "";

    [DataField]
    public string StaminaIcon = "";

    [DataField]
    public LocId ChancePopup = "";

    [DataField]
    public LocId StaminaPopup = "";
}

public sealed partial class CycleDodgeModeEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class DodgeEvent : EntityEventArgs
{
    public NetEntity Entity;

    public DodgeEvent(NetEntity entity)
    {
        Entity = entity;
    }
}
