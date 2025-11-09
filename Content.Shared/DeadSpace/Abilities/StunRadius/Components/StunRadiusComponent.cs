// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.DeadSpace.Abilities.StunRadius.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class StunRadiusComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilEndAnimation = TimeSpan.Zero;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float DurationAnimation = 0f;

    [DataField]
    public string EffectPrototype = string.Empty;

    [DataField]
    public EntProtoId ActionStunRadius = "ActionStunRadius";

    [DataField]
    public EntityUid? ActionStunRadiusEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    [Access(Other = AccessPermissions.ReadWrite)]
    public float ParalyzeTime = 3f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    [Access(Other = AccessPermissions.ReadWrite)]
    public float RangeStun = 5f;

    [DataField]
    public SoundSpecifier? StunRadiusSound = default;

    [DataField]
    public bool IgnorAlien = true;

    public bool IsRunning = false;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float BaseRadialAcceleration = -10f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float BaseTangentialAcceleration = 0f;

    [DataField]
    public float Strenght = 0f;

    #region Visualizer
    [DataField]
    public string State = string.Empty;

    [DataField]
    public string StunState = string.Empty;
    #endregion

    [DataField]
    public bool StunBorg = false;

}
