// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Temperature.Components;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingAbsoluteFreezingVeinsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingAbsoluteFreezingVeinsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShadowlingAbsoluteFreezingVeinsComponent, ShadowlingAbsoluteFreezingVeinsEvent>(OnAbsoluteFreezingVeinsAction);
    }

    private void OnComponentInit(EntityUid uid, ShadowlingAbsoluteFreezingVeinsComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionAbsoluteFreezingVeinsEntity, component.ActionAbsoluteFreezingVeins);
    }

    private void OnAbsoluteFreezingVeinsAction(EntityUid uid, ShadowlingAbsoluteFreezingVeinsComponent component, ShadowlingAbsoluteFreezingVeinsEvent args)
    {
        if (args.Handled) return;

        var target = args.Target;

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return;

        if (HasComp<ShadowlingComponent>(target) ||
            HasComp<ShadowlingRevealComponent>(target) ||
            HasComp<ShadowlingSlaveComponent>(target))
            return;

        var meta = MetaData(target);
        if (meta.EntityPrototype?.ID == component.ImmunePrototypeId)
            return;

        if (TryComp<TemperatureComponent>(target, out var temp))
        {
            temp.CurrentTemperature = component.TemperatureSet;
        }

        DamageSpecifier damage = new();
        damage.DamageDict.Add("Cold", component.DamageCold);
        _damageable.TryChangeDamage(target, damage, ignoreResistances: false, origin: uid);

        _popup.PopupEntity("Ваша кровь замерзает!", target, target, PopupType.LargeCaution);

        args.Handled = true;
    }
}