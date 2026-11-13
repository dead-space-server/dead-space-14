// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Light.Components;
using Content.Server.DeadSpace.Abilities.Cocoon.Components;
using Content.Server.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Body.Events;
using Content.Shared.Destructible;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Gibbing;

namespace Content.Server.DeadSpace.Demons.LockCocoon;

public sealed partial class ShadowCocoonSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPointLightSystem _pointLightSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> FlareTag = "Flare";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowCocoonComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<ShadowCocoonComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShadowCocoonComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<ShadowCocoonComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShadowCocoonComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ShadowCocoonComponent, DestructionEventArgs>(OnDestruction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shadowCocoonComponent = EntityQueryEnumerator<ShadowCocoonComponent>();
        while (shadowCocoonComponent.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime > component.NextTick)
            {
                TurnOffElectricity(uid, component);
            }
        }
    }

    private void TurnOffElectricity(EntityUid uid, ShadowCocoonComponent component)
    {
        var xform = Transform(uid);

        var entities = _lookup.GetEntitiesInRange<PointLightComponent>(_transform.GetMapCoordinates(uid, xform), component.Range);
        var lights = new HashSet<EntityUid>();

        foreach (var (entity, pointLightComp) in entities)
        {
            if (CheckParentVisibilityLayer(entity))
                continue;

            if (IsPortableRevealingLight(entity, pointLightComp))
            {
                component.PointEntities.Remove(entity);
                RestoreLightVisuals(entity, component);
                continue;
            }

            lights.Add(entity);
            component.PointEntities.TryAdd(entity, pointLightComp.Enabled);
            TurnOffLightVisuals(entity, component);
            _pointLightSystem.SetEnabled(entity, false);
        }

        foreach (var lightState in new Dictionary<EntityUid, bool>(component.PointEntities))
        {
            var entity = lightState.Key;

            if (!lights.Contains(entity))
            {
                if (TryComp<PointLightComponent>(entity, out var poweredLight))
                {
                    _pointLightSystem.SetEnabled(entity, lightState.Value, poweredLight);
                }

                RestoreLightVisuals(entity, component);
                component.PointEntities.Remove(entity);
            }
        }

        component.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
    }

    private bool IsPortableRevealingLight(EntityUid uid, PointLightComponent light)
    {
        if (!light.Enabled)
            return false;

        if (TryComp<HandheldLightComponent>(uid, out var handheldLight))
            return handheldLight.Activated;

        return TryComp<ExpendableLightComponent>(uid, out var expendableLight) &&
               expendableLight.Activated &&
               _tag.HasTag(uid, FlareTag);
    }

    /// <summary>
    /// Данный метод удаляет сущность из контейнера, в который ее вставили.
    /// Необходимо для удаления возможности перемещать теневые коконы в ящиках/шкафах.
    /// </summary>
    private void OnInserted(EntityUid uid, ShadowCocoonComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<EntityStorageComponent>(args.Container.Owner))
            return;

        // Откладываем удаление на следующий тик! Иначе может возникнуть проблема с флагами системы контейнеров
        Timer.Spawn(0, () =>
        {
            _popupSystem.PopupEntity(
                "Стены хранилища не могут удержать эту материю",
                args.Container.Owner,
                PopupType.MediumCaution
            );
            _containerSystem.Remove(uid, args.Container);
        });
    }

    /// <summary>  
    /// Проверяет, отличается ли слой видимости родителя от Normal  
    /// </summary>  
    public bool CheckParentVisibilityLayer(EntityUid uid)
    {
        if (!_containerSystem.TryGetContainingContainer((uid, null, null), out var container))
            return false;

        var parentUid = container.Owner;

        if (TryComp<VisibilityComponent>(parentUid, out var visibilityComponent))
        {
            return visibilityComponent.Layer != (int)VisibilityFlags.Normal;
        }

        if (HasComp<GhostComponent>(parentUid))
            return true;

        return false;
    }

    private void OnInit(EntityUid uid, ShadowCocoonComponent component, ComponentInit args)
    {
        component.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnMapInit(EntityUid uid, ShadowCocoonComponent component, MapInitEvent args)
    {
        if (!HasComp<CocoonComponent>(uid))
            AddComp<CocoonComponent>(uid);
    }

    private void OnGibbed(EntityUid uid, ShadowCocoonComponent component, BeingGibbedEvent args)
    {
        DestroyCocoon(uid, component);
    }

    private void OnShutDown(EntityUid uid, ShadowCocoonComponent component, ComponentShutdown args)
    {
        DestroyCocoon(uid, component);
    }

    private void OnDestruction(EntityUid uid, ShadowCocoonComponent component, DestructionEventArgs args)
    {
        DestroyCocoon(uid, component);
    }

    private void DestroyCocoon(EntityUid uid, ShadowCocoonComponent component)
    {
        foreach (var (entity, wasEnabled) in component.PointEntities)
        {
            if (TryComp<PointLightComponent>(entity, out _))
            {
                _pointLightSystem.SetEnabled(entity, wasEnabled);
            }
        }

        foreach (var entity in new List<EntityUid>(component.PoweredLightStates.Keys))
        {
            RestoreLightVisuals(entity, component);
        }
    }

    private void TurnOffLightVisuals(EntityUid uid, ShadowCocoonComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearance.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state, appearance) ||
            state != PoweredLightState.On)
        {
            return;
        }

        component.PoweredLightStates.TryAdd(uid, state);
        _appearance.SetData(uid, PoweredLightVisuals.BulbState, PoweredLightState.Off, appearance);
    }

    private void RestoreLightVisuals(EntityUid uid, ShadowCocoonComponent component)
    {
        if (!component.PoweredLightStates.Remove(uid, out var state) ||
            !TryComp<AppearanceComponent>(uid, out var appearance))
        {
            return;
        }

        if (TryComp<PoweredLightComponent>(uid, out var poweredLight) && !poweredLight.CurrentLit)
            return;

        _appearance.SetData(uid, PoweredLightVisuals.BulbState, state, appearance);
    }
}
