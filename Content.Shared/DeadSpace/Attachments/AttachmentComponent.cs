// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Attachments;

/// <summary>
/// An item that changes the weapon it is put into (any <see cref="AttachmentHolderComponent"/> slot).
/// Everything is optional, so one prototype can be a stat mod, a component swap, an on-hit effect or all of them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AttachmentComponent : Component
{
    /// <summary>Only one attachment of a group fits in a weapon (e.g. two goliath tentacles).</summary>
    [DataField]
    public string? UniqueGroup;

    /// <summary>Shown when examining the weapon and the attachment itself.</summary>
    [DataField]
    public LocId? Description;

    #region Components

    /// <summary>
    /// Added to the weapon while attached. A component the weapon already has is replaced
    /// and restored as it was when the attachment is removed.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();

    /// <summary>Removed from the weapon while attached, restored afterwards.</summary>
    [DataField]
    public ComponentRegistry RemoveComponents = new();

    #endregion

    #region Melee

    /// <summary>Added to every melee hit.</summary>
    [DataField]
    public DamageSpecifier? BonusDamage;

    /// <summary>Added to melee hits only while the weapon is wielded.</summary>
    [DataField]
    public DamageSpecifier? WieldedBonusDamage;

    /// <summary>Multiplies the whole melee damage after bonuses.</summary>
    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public float AttackRateMultiplier = 1f;

    [DataField]
    public float BonusAttackRate;

    [DataField]
    public float RangeMultiplier = 1f;

    [DataField]
    public float BonusRange;

    #endregion

    #region Ranged

    [DataField]
    public float FireRateMultiplier = 1f;

    [DataField]
    public float ProjectileSpeedMultiplier = 1f;

    /// <summary>Added to the recharge time of self-recharging ammo (crusher marker). Negative is faster.</summary>
    [DataField]
    public float RechargeCooldownBonus;

    #endregion

    #region Effects

    /// <summary>Entity effects applied to everything hit in melee (ignite, cool down, ...).</summary>
    [DataField]
    public List<EntityEffect> TargetHitEffects = new();

    /// <summary>Custom effects on every melee hit.</summary>
    [DataField]
    public List<AttachmentEffect> HitEffects = new();

    /// <summary>Custom effects when a crusher mark is detonated with this weapon.</summary>
    [DataField]
    public List<AttachmentEffect> MarkDetonationEffects = new();

    #endregion

    #region Visuals

    /// <summary>Layers drawn over the weapon while attached.</summary>
    [DataField]
    public List<AttachmentLayer> Layers = new();

    #endregion
}

[DataDefinition]
public sealed partial class AttachmentLayer
{
    /// <summary>State to draw. Taken from <see cref="Sprite"/>, or from the weapon's own RSI when that is null.</summary>
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public ResPath? Sprite;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public bool Unshaded;
}
