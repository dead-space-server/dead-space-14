// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Power.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.Damage.Systems;
using Content.Server.Light.Components;
using Content.Shared.Light;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Item;
using Content.Shared.IgnitionSource;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingVeilSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedIgnitionSourceSystem _ignitionSource = default!;
    private const string ActionVeilId = "ActionShadowlingVeil";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingVeilComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShadowlingVeilComponent, ShadowlingVeilActionEvent>(OnShadowlingVeil);
        SubscribeLocalEvent<ShadowlingVeilComponent, ComponentShutdown>(OnVeilShutdown);
        SubscribeLocalEvent<HandheldLightComponent, LightToggleEvent>(OnHandheldLightToggled);
    }

    private void OnMapInit(EntityUid uid, ShadowlingVeilComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ActionVeilId);
    }

    private void OnHandheldLightToggled(EntityUid uid, HandheldLightComponent component, LightToggleEvent args)
    {
        if (!args.IsOn)
            return;

        var lightPos = _transform.GetMapCoordinates(uid);
        var veilQuery = EntityQueryEnumerator<ShadowlingVeilComponent, TransformComponent>();
        while (veilQuery.MoveNext(out var veilComp, out var veilXform))
        {
            if (!veilComp.VeilActive)
                continue;

            var veilPos = _transform.GetMapCoordinates(veilXform);
            if (veilPos.MapId != lightPos.MapId)
                continue;

            if ((veilPos.Position - lightPos.Position).Length() > 10f)
                continue;

            _handheldLight.TurnOff((uid, component));
            if (!veilComp.WereActivated.Contains(uid))
                veilComp.WereActivated.Add(uid);
            break;
        }
    }

    private void KillFlare(EntityUid entity)
    {
        if (!TryComp<ExpendableLightComponent>(entity, out var expendable))
            return;

        if (!expendable.Activated)
            return;

        if (TryComp<PointLightComponent>(entity, out var expLight))
            _light.SetEnabled(entity, false, expLight);

        if (TryComp<IgnitionSourceComponent>(entity, out var ignition))
            _ignitionSource.SetIgnited((entity, ignition), false);

        expendable.CurrentState = ExpendableLightState.Dead;
        Dirty(entity, expendable);

        if (TryComp<AppearanceComponent>(entity, out var appearance))
        {
            _appearance.SetData(entity, ExpendableLightVisuals.State, ExpendableLightState.Dead, appearance);
            _appearance.SetData(entity, ExpendableLightVisuals.Behavior, string.Empty, appearance);
        }

        if (TryComp<ItemComponent>(entity, out var item))
            _item.SetHeldPrefix(entity, "unlit", false, item);
    }

    private void OnShadowlingVeil(EntityUid uid, ShadowlingVeilComponent component, ShadowlingVeilActionEvent args)
    {
        if (args.Handled || component.VeilActive) return;

        var worldPos = _transform.GetMapCoordinates(uid);
        var allEntities = _lookup.GetEntitiesInRange(worldPos, 10f);
        component.AffectedLights.Clear();
        component.WereActivated.Clear();

        foreach (var entity in allEntities)
        {
            if (_container.TryGetContainer(entity, "light_bulb", out var container))
            {
                if (container.ContainedEntities.Count > 0)
                {
                    bool hasIntactBulb = false;
                    foreach (var bulbUid in container.ContainedEntities)
                    {
                        if (TryComp<LightBulbComponent>(bulbUid, out var bulb) && bulb.State == LightBulbState.Normal)
                        {
                            hasIntactBulb = true;
                            break;
                        }
                    }

                    if (hasIntactBulb)
                    {
                        var damage = new DamageSpecifier();
                        damage.DamageDict.Add("Blunt", 20);
                        _damageable.TryChangeDamage(entity, damage, true);
                        Spawn("EffectSparks", Transform(entity).Coordinates);
                        component.AffectedLights.Add(entity);
                    }
                }
                continue;
            }

            if (HasComp<ExpendableLightComponent>(entity))
            {
                if (TryComp<ExpendableLightComponent>(entity, out var exp) && exp.Activated)
                    component.WereActivated.Add(entity);
                KillFlare(entity);
                component.AffectedLights.Add(entity);
                continue;
            }

            if (TryComp<HandheldLightComponent>(entity, out var handheld))
            {
                if (handheld.Activated)
                {
                    component.WereActivated.Add(entity);
                    _handheldLight.TurnOff((entity, handheld));
                }
                component.AffectedLights.Add(entity);
                continue;
            }

            if (TryComp<ItemTogglePointLightComponent>(entity, out _) &&
                TryComp<ItemToggleComponent>(entity, out var itemToggle))
            {
                if (itemToggle.Activated)
                {
                    component.WereActivated.Add(entity);
                    _itemToggle.Toggle((entity, itemToggle));
                }
                component.AffectedLights.Add(entity);
                continue;
            }

            if (TryComp<PointLightComponent>(entity, out var light))
            {
                if (light.Enabled)
                {
                    component.WereActivated.Add(entity);
                    _light.SetEnabled(entity, false, light);
                }
                component.AffectedLights.Add(entity);
            }
        }

        component.VeilActive = true;
        component.VeilTimer = 10f;
        _popup.PopupEntity("Тьма поглощает свет!", uid, uid, PopupType.Large);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ShadowlingVeilComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.VeilActive) continue;

            comp.VeilTimer -= frameTime;

            var worldPos = _transform.GetMapCoordinates(uid);
            var allNearby = _lookup.GetEntitiesInRange(worldPos, 10f);

            foreach (var entity in allNearby)
            {
                if (comp.AffectedLights.Contains(entity))
                    continue;

                if (_container.TryGetContainer(entity, "light_bulb", out var container))
                {
                    if (container.ContainedEntities.Count > 0)
                    {
                        bool hasIntactBulb = false;
                        foreach (var bulbUid in container.ContainedEntities)
                        {
                            if (TryComp<LightBulbComponent>(bulbUid, out var bulb) && bulb.State == LightBulbState.Normal)
                            {
                                hasIntactBulb = true;
                                break;
                            }
                        }

                        if (hasIntactBulb)
                        {
                            var damage = new DamageSpecifier();
                            damage.DamageDict.Add("Blunt", 20);
                            _damageable.TryChangeDamage(entity, damage, true);
                            Spawn("EffectSparks", Transform(entity).Coordinates);
                            comp.AffectedLights.Add(entity);
                        }
                    }
                    continue;
                }

                if (HasComp<ExpendableLightComponent>(entity))
                {
                    KillFlare(entity);
                    comp.AffectedLights.Add(entity);
                    continue;
                }

                if (TryComp<HandheldLightComponent>(entity, out var handheld) && handheld.Activated)
                {
                    _handheldLight.TurnOff((entity, handheld));
                    comp.AffectedLights.Add(entity);
                    if (!comp.WereActivated.Contains(entity))
                        comp.WereActivated.Add(entity);
                    continue;
                }

                if (TryComp<ItemTogglePointLightComponent>(entity, out _) &&
                    TryComp<ItemToggleComponent>(entity, out var itemToggle) && itemToggle.Activated)
                {
                    _itemToggle.Toggle((entity, itemToggle));
                    comp.AffectedLights.Add(entity);
                    if (!comp.WereActivated.Contains(entity))
                        comp.WereActivated.Add(entity);
                    continue;
                }

                if (TryComp<PointLightComponent>(entity, out var light) && light.Enabled)
                {
                    _light.SetEnabled(entity, false, light);
                    comp.AffectedLights.Add(entity);
                    if (!comp.WereActivated.Contains(entity))
                        comp.WereActivated.Add(entity);
                }
            }

            foreach (var lightUid in comp.AffectedLights)
            {
                if (!Exists(lightUid))
                    continue;

                if (TryComp<ExpendableLightComponent>(lightUid, out var exp) && exp.Activated)
                {
                    KillFlare(lightUid);
                    continue;
                }

                if (TryComp<HandheldLightComponent>(lightUid, out var hh) && hh.Activated)
                {
                    _handheldLight.TurnOff((lightUid, hh));
                    continue;
                }

                if (TryComp<ItemTogglePointLightComponent>(lightUid, out _) &&
                    TryComp<ItemToggleComponent>(lightUid, out var it) && it.Activated)
                {
                    _itemToggle.Toggle((lightUid, it));
                    continue;
                }

                if (TryComp<PointLightComponent>(lightUid, out var existingLight) && existingLight.Enabled)
                    _light.SetEnabled(lightUid, false, existingLight);
            }

            if (comp.VeilTimer <= 0)
            {
                RestoreLights(comp);
                _popup.PopupEntity("Тьма рассеивается!", uid, uid, PopupType.Large);
                comp.AffectedLights.Clear();
                comp.WereActivated.Clear();
                comp.VeilActive = false;
            }
        }
    }

    private void OnVeilShutdown(EntityUid uid, ShadowlingVeilComponent component, ComponentShutdown args)
    {
        if (!component.VeilActive)
            return;

        RestoreLights(component);
        component.AffectedLights.Clear();
        component.WereActivated.Clear();
        component.VeilActive = false;
    }

    private void RestoreLights(ShadowlingVeilComponent component)
    {
        foreach (var lightUid in component.AffectedLights)
        {
            if (!Exists(lightUid)) continue;

            if (HasComp<HandheldLightComponent>(lightUid))
                continue;

            if (HasComp<ItemTogglePointLightComponent>(lightUid))
            {
                if (component.WereActivated.Contains(lightUid) &&
                    TryComp<ItemToggleComponent>(lightUid, out var it) && !it.Activated)
                    _itemToggle.Toggle((lightUid, it));
                continue;
            }

            if (HasComp<ExpendableLightComponent>(lightUid))
                continue;

            if (!component.WereActivated.Contains(lightUid))
                continue;

            bool canEnable = true;
            if (TryComp<ApcPowerReceiverComponent>(lightUid, out var power) && !power.Powered)
                canEnable = false;
            if (TryComp<UnpoweredFlashlightComponent>(lightUid, out var unp) && !unp.LightOn)
                canEnable = false;

            if (canEnable)
                _light.SetEnabled(lightUid, true);
        }
    }
}