// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Alert;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Shared.Input.Binding;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Weapons.Parry;

public sealed class ParrySystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly INetManager _net = default!;

    private static readonly ProtoId<TagPrototype> ParryAllTag = "ParryAll";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, BeforeMeleeDamageEvent>(OnBeforeMeleeDamage);
        SubscribeLocalEvent<ParryComponent, HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<ParryComponent, HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<ParryComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<ParryComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeAllEvent<ParryPressedEvent>(OnParryPressed);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Parry, InputCmdHandler.FromDelegate(OnParryPressedLocal))
            .Register<ParrySystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ParrySystem>();
    }

    private void OnParryPressedLocal(ICommonSession? session)
    {
        if (!_net.IsClient || session?.AttachedEntity is not { } user ||
            !TryGetParryWeapon(user, out var weapon))
        {
            return;
        }

        RaisePredictiveEvent(new ParryPressedEvent(GetNetEntity(weapon.Owner)));
    }

    private void OnParryPressed(ParryPressedEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user ||
            !TryGetParryWeapon(user, out var weapon) ||
            weapon.Owner != GetEntity(args.Weapon) ||
            weapon.Comp.NextParry > _timing.CurTime)
        {
            return;
        }

        weapon.Comp.CooldownStart = _timing.CurTime;
        weapon.Comp.ActiveUntil = _timing.CurTime + weapon.Comp.ParryWindow;
        weapon.Comp.NextParry = weapon.Comp.ActiveUntil + weapon.Comp.FailureCooldown;
        Dirty(weapon);
        UpdateAlert(user, weapon.Comp);
    }

    private void OnBeforeMeleeDamage(Entity<HandsComponent> defender, ref BeforeMeleeDamageEvent args)
    {
        if (args.Attacker == defender.Owner ||
            !TryGetParryWeapon(defender.Owner, out var parryWeapon) ||
            parryWeapon.Comp.ActiveUntil <= _timing.CurTime ||
            !CanParry(parryWeapon, args.Weapon))
        {
            return;
        }

        SetCooldown(parryWeapon, parryWeapon.Comp.SuccessCooldown);
        parryWeapon.Comp.ActiveUntil = TimeSpan.Zero;
        Dirty(parryWeapon);
        args.Cancelled = true;
        if (_net.IsServer)
            KnockBackAttacker(args.Attacker, defender.Owner, parryWeapon.Comp);

        UpdateAlert(defender.Owner, parryWeapon.Comp);

        if (_net.IsServer)
        {
            _audio.PlayPvs(parryWeapon.Comp.SuccessSound, defender.Owner);
            var popup = _random.Prob(parryWeapon.Comp.RarePopupChance)
                ? "parry-success-rare-popup"
                : "parry-success-popup";
            _popup.PopupEntity(Loc.GetString(popup), defender.Owner, PopupType.LargeCaution);
        }
    }

    private bool CanParry(Entity<ParryComponent> parryWeapon, EntityUid attackingWeapon)
    {
        if (_tag.HasTag(parryWeapon, ParryAllTag))
            return true;

        return !_whitelist.IsWhitelistFail(parryWeapon.Comp.ParryableWeapons, attackingWeapon);
    }

    private void OnHandSelected(Entity<ParryComponent> weapon, ref HandSelectedEvent args)
    {
        if (TryGetParryWeapon(args.User, out var selected) && selected.Owner == weapon.Owner)
            UpdateAlert(args.User, weapon.Comp);
        else
            _alerts.ClearAlert(args.User, weapon.Comp.CooldownAlert);
    }

    private void OnHandDeselected(Entity<ParryComponent> weapon, ref HandDeselectedEvent args)
    {
        _alerts.ClearAlert(args.User, weapon.Comp.CooldownAlert);
    }

    private void OnUnequippedHand(Entity<ParryComponent> weapon, ref GotUnequippedHandEvent args)
    {
        if (!TryGetParryWeapon(args.User, out _))
            _alerts.ClearAlert(args.User, weapon.Comp.CooldownAlert);
    }

    private void OnItemToggled(Entity<ParryComponent> weapon, ref ItemToggledEvent args)
    {
        if (!args.Activated && weapon.Comp.RequiresActiveToggle)
        {
            weapon.Comp.ActiveUntil = TimeSpan.Zero;
            Dirty(weapon);
        }

        if (args.User is not { } user)
            return;

        if (TryGetParryWeapon(user, out var active) && active.Owner == weapon.Owner)
            UpdateAlert(user, weapon.Comp);
        else
            _alerts.ClearAlert(user, weapon.Comp.CooldownAlert);
    }

    private void UpdateAlert(EntityUid user, ParryComponent component)
    {
        var cooldown = component.NextParry > _timing.CurTime
            ? (component.CooldownStart, component.NextParry)
            : ((TimeSpan, TimeSpan)?) null;

        _alerts.ShowAlert(user, component.CooldownAlert, cooldown: cooldown, autoRemove: false);
    }

    private bool TryGetParryWeapon(EntityUid defender, out Entity<ParryComponent> parryWeapon)
    {
        parryWeapon = default;
        if (!TryComp<HandsComponent>(defender, out var hands) ||
            !_hands.TryGetActiveItem((defender, hands), out var held) ||
            !TryComp<ParryComponent>(held, out var parry) ||
            !HasComp<MeleeWeaponComponent>(held) ||
            parry.RequiresActiveToggle && !_itemToggle.IsActivated(held.Value))
        {
            return false;
        }

        parryWeapon = (held.Value, parry);
        return true;
    }

    private void KnockBackAttacker(EntityUid attacker, EntityUid defender, ParryComponent component)
    {
        var direction = _transform.GetWorldPosition(attacker) - _transform.GetWorldPosition(defender);
        if (direction == Vector2.Zero)
            return;

        _throwing.TryThrow(
            attacker,
            direction.Normalized() * component.KnockbackDistance,
            component.KnockbackSpeed,
            defender,
            compensateFriction: false,
            recoil: false,
            playSound: false,
            doSpin: false);
    }

    private void SetCooldown(Entity<ParryComponent> weapon, TimeSpan cooldown)
    {
        weapon.Comp.CooldownStart = _timing.CurTime;
        weapon.Comp.NextParry = _timing.CurTime + cooldown;
        Dirty(weapon);
    }
}

[Serializable, NetSerializable]
public sealed class ParryPressedEvent(NetEntity weapon) : EntityEventArgs
{
    public NetEntity Weapon = weapon;
}
