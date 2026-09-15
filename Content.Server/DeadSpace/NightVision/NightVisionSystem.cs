using Content.Shared.Actions;
using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.DeadSpace.NightVision;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<NightVisionComponent, ComponentGetState>(OnNightVisionGetState);
        SubscribeLocalEvent<NightVisionComponent, ToggleNightVisionActionEvent>(OnToggleNightVision);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NightVisionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsNightVision || comp.Duration == null || comp.RemainingTime == null)
                continue;

            comp.RemainingTime -= frameTime;

            if (comp.RemainingTime <= 0f)
            {
                comp.RemainingTime = null;
                DisableNightVision(uid, comp);
            }
        }
    }

    private void OnNightVisionGetState(EntityUid uid, NightVisionComponent component, ref ComponentGetState args)
    {
        args.State = new NightVisionComponentState(
            component.Color,
            component.IsNightVision,
            _timing.CurTick.Value,
            component.ActivateSound,
            component.Animation,
            component.Duration,
            component.Desaturation);
    }

    private void OnMapInit(EntityUid uid, NightVisionComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionToggleNightVisionEntity, component.ActionToggleNightVision);
    }

    private void OnComponentRemove(EntityUid uid, NightVisionComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, component.ActionToggleNightVisionEntity);
    }

    private void OnToggleNightVision(EntityUid uid, NightVisionComponent component, ToggleNightVisionActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleNightVision(uid, component);
    }

    private void ToggleNightVision(EntityUid uid, NightVisionComponent component)
    {
        component.IsNightVision = !component.IsNightVision;

        if (component.IsNightVision && component.Duration != null)
        {
            component.RemainingTime = component.Duration;
        }
        else
        {
            component.RemainingTime = null;
        }

        Dirty(uid, component);
    }

    private void DisableNightVision(EntityUid uid, NightVisionComponent component)
    {
        component.IsNightVision = false;
        component.RemainingTime = null;
        Dirty(uid, component);
    }
}
