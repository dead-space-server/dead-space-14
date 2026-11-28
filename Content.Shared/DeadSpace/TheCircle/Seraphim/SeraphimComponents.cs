// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.TheCircle.Seraphim;

/// <summary>
/// Seraphim pick: after a short procedure an ally's organs, bones and torn flesh are made whole.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeraphimMendComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSeraphimMend";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Starts only once a procedure is finished, so an interrupted one costs nothing.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Seraphim pick: a necromorph fibre tether to an ally. Healing either of them gets is shared with the other,
/// the ally's overdoses are split with the seraphim, and both slowly mend fractures and bleeding.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeraphimBondComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSeraphimBond";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? Partner;

    /// <summary>
    /// The tether snaps when the two are further apart than this.
    /// </summary>
    [DataField]
    public float MaxDistance = 8f;

    /// <summary>
    /// Share of healing one of them gets that the other gets as well.
    /// </summary>
    [DataField]
    public float HealShare = 1f;

    /// <summary>
    /// Bone damage mended on each limb per second (a full break is 40).
    /// </summary>
    [DataField]
    public float BoneMendPerSecond = 0.5f;

    /// <summary>
    /// Bleeding stopped on each limb per second.
    /// </summary>
    [DataField]
    public float BleedMendPerSecond = 0.02f;

    [DataField]
    public SpriteSpecifier Rope = new SpriteSpecifier.Rsi(new ResPath("_DeadSpace/Necromorfs/TheCircle/Weapon/melee/hand-hook.rsi"), "rope");

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>
/// Put on the ally a seraphim is tethered to.
/// </summary>
[RegisterComponent]
public sealed partial class SeraphimBondedComponent : Component
{
    [ViewVariables]
    public EntityUid Seraphim;
}

public sealed partial class SeraphimMendActionEvent : EntityTargetActionEvent;

public sealed partial class SeraphimBondActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class SeraphimMendDoAfterEvent : SimpleDoAfterEvent;
