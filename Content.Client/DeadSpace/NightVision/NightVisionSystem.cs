using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Shared.GameTicking;
using Robust.Shared.Player;
using Content.Shared.DeadSpace.NightVision;
using Content.Client.DeadSpace.Components.NightVision;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] ILightManager _lightManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private NightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentInit>(OnNightVisionInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnNightVisionShutdown);

        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
        SubscribeLocalEvent<NightVisionComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<NightVisionComponent, ToggleNightVisionActionEvent>(OnToggleNightVision);

        _overlay = new();
    }

    private void OnToggleNightVision(EntityUid uid, NightVisionComponent component, ref ToggleNightVisionActionEvent args)
    {
        if (args.Handled || component.IsToggled)
            return;

        args.Handled = true;

        component.ClientLastToggleTick = _timing.CurTick.Value;
        component.IsToggled = true;

        ToggleNightVision(uid, component, !component.IsNightVision);
    }

    private void ToggleNightVision(EntityUid uid, NightVisionComponent component, bool active)
    {
        if (_player.LocalEntity != uid)
            return;

        component.IsNightVision = active;
    }

    private void OnHandleState(EntityUid uid, NightVisionComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NightVisionComponentState state)
            return;

        component.Color = state.Color;
        component.ServerLastToggleTick = state.LastToggleTick;

        // Применяю серверное состояние, если оно свежее предсказанного клиентом
        if (component.ClientLastToggleTick > component.ServerLastToggleTick)
            return;

        component.IsToggled = false;

        if (component.IsNightVision == state.IsNightVision)
            return;

        ToggleNightVision(uid, component, state.IsNightVision);

        component.ClientLastToggleTick = component.ServerLastToggleTick;
    }

    private void OnPlayerAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
        _lightManager.DrawLighting = true;
    }

    private void OnNightVisionInit(EntityUid uid, NightVisionComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnNightVisionShutdown(EntityUid uid, NightVisionComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _lightManager.DrawLighting = true;
        }
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lightManager.DrawLighting = true;
    }
}
