// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Cleaving saw from the blood-drunk miner: closed it attacks rapidly and piles bleed on fauna and bosses; open it
/// swings slowly, cleaves for bonus damage, and only open does the built-up bleed burst - its finishing blow.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandCleavingSawComponent : Component
{
    /// <summary>Attack rate multiplier while the saw is closed.</summary>
    [DataField]
    public float ClosedAttackRateMultiplier = 2f;

    /// <summary>Attack rate multiplier while the saw is open: slow, heavy swings.</summary>
    [DataField]
    public float OpenAttackRateMultiplier = 0.6f;
}

[Serializable, NetSerializable]
public sealed partial class LavalandLavaStaffDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates TargetCoordinates;

    private LavalandLavaStaffDoAfterEvent()
    {
    }

    public LavalandLavaStaffDoAfterEvent(NetCoordinates target)
    {
        TargetCoordinates = target;
    }

    public override DoAfterEvent Clone() => this;
}

public sealed class LavalandCleavingSawSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandCleavingSawComponent, GetMeleeAttackRateEvent>(OnGetAttackRate);
    }

    private void OnGetAttackRate(Entity<LavalandCleavingSawComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        args.Multipliers *= _toggle.IsActivated(ent.Owner)
            ? ent.Comp.OpenAttackRateMultiplier
            : ent.Comp.ClosedAttackRateMultiplier;
    }
}
