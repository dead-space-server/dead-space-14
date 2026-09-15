using Content.Server.DeadSpace.Abilities.Cocoon.Components;
using Robust.Shared.Containers;
using Content.Server.Body.Components;
using Robust.Shared.Timing;
using Content.Shared.Destructible;
using Content.Server.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Body.Events;
using Content.Shared.Gibbing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Abilities.Cocoon;

public sealed class CocoonSystem : EntitySystem
{
    private static readonly EntProtoId MutedEffect = "StatusEffectCocoonMuted";
    private static readonly EntProtoId PressureImmunityEffect = "StatusEffectPressureImmunity"; // DS14 - PressureImmunity status effect migration

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    private ISawmill _sawmill = default!;

    const float Factor = 1f;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("CocoonSystem");

        SubscribeLocalEvent<CocoonComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<CocoonComponent, InsertIntoCocoonEvent>(OnInsert);
        SubscribeLocalEvent<CocoonComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CocoonComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<CocoonComponent, DestructionEventArgs>(OnDestruction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cocoons = EntityQueryEnumerator<CocoonComponent>();
        while (cocoons.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime > component.NextTick)
            {
                UpdateCocoon(uid, component);
            }
        }
    }

    public bool IsEntityInCocoon(EntityUid uid, EntityUid target, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return _container.IsEntityInContainer(target);
    }

    private void OnMapInit(EntityUid uid, CocoonComponent component, MapInitEvent args)
    {
        component.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
        component.Cocoon = _container.EnsureContainer<Container>(uid, "cocoon");
    }

    private void OnInsert(EntityUid uid, CocoonComponent component, InsertIntoCocoonEvent args)
    {
        var target = args.Target;

        Insert(uid, target, component);
    }

    private void OnGibbed(EntityUid uid, CocoonComponent component, BeingGibbedEvent args)
    {
        EmptyCocoon(uid);
    }

    private void OnShutDown(EntityUid uid, CocoonComponent component, ComponentShutdown args)
    {
        EmptyCocoon(uid);
    }

    private void OnDestruction(EntityUid uid, CocoonComponent component, DestructionEventArgs args)
    {
        EmptyCocoon(uid);
    }

    public bool TryInsertCocoon(EntityUid uid, EntityUid target, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (_container.IsEntityOrParentInContainer(target))
            return false;

        var insertIntoCocoon = new InsertIntoCocoonEvent(target);
        RaiseLocalEvent(uid, ref insertIntoCocoon);

        return true;
    }

    public EntityUid? GetPrisoner(EntityUid uid, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return null;

        return component.Prisoner;
    }

    public void EmptyCocoon(EntityUid uid, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var target = component.Prisoner;

        if (target == null)
        {
            _sawmill.Warning("Prisoner target is null in EmptyCocoon.");
            return;
        }

        if (IsEntityInCocoon(uid, target.Value, component))
            _container.EmptyContainer(component.Cocoon);

        if (!component.IsHermetically)
            return;

        _statusEffects.TryRemoveStatusEffect(target.Value, MutedEffect);

        if (HasComp<TemporaryBlindnessComponent>(target) && !component.Blindable)
            RemComp<TemporaryBlindnessComponent>(target.Value);

        if (HasComp<PacifiedComponent>(target) && !component.Pacified)
            RemComp<PacifiedComponent>(target.Value);

        // DS14-start: PressureImmunity is a status effect on the current upstream baseline.
        if (_statusEffects.HasStatusEffect(target.Value, PressureImmunityEffect) && !component.Pressure)
        {
            _sawmill.Info("Adding BarotraumaComponent back to target.");
            _statusEffects.TryRemoveStatusEffect(target.Value, PressureImmunityEffect);
        }
        else
        {
            _sawmill.Warning("BarotraumaComponent is either already present or null.");
        }
        // DS14-end
    }

    public void UpdateCocoon(EntityUid uid, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.Prisoner == null)
            return;

        if (!component.IsHermetically)
            return;

        if (TryComp<RespiratorComponent>(component.Prisoner, out var resp))
        {
            _respirator.UpdateSaturation(component.Prisoner.Value, Factor, resp);
        }

        component.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
    }

    private void Insert(EntityUid uid, EntityUid target, CocoonComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        _container.Insert(target, component.Cocoon);

        component.Prisoner = target;

        _statusEffects.TrySetStatusEffectDuration(target, MutedEffect);

        if (!HasComp<PacifiedComponent>(target))
            AddComp<PacifiedComponent>(target);
        else
            component.Pacified = true;

        if (!HasComp<TemporaryBlindnessComponent>(target))
            AddComp<TemporaryBlindnessComponent>(target);
        else
            component.Blindable = true;


        if (!component.IsHermetically)
            return;

        // DS14-start: PressureImmunity is a status effect on the current upstream baseline.
        if (!_statusEffects.HasStatusEffect(target, PressureImmunityEffect))
            _statusEffects.TrySetStatusEffectDuration(target, PressureImmunityEffect);
        else
            component.Pressure = true;
        // DS14-end
    }
}
