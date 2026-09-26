// Dead Space 14, licensed under LICENSE.TXT
using Content.Client.Hands.Systems;
using Content.Shared.DeadSpace.TheCircle.ArchitectArm;
using Content.Shared.Interaction;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.ArchitectArm;

public sealed class ArchitectArmSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ArchitectArmWindupOverlay _windup = default!;

    public override void Initialize()
    {
        base.Initialize();
        _windup = new ArchitectArmWindupOverlay(_timing, _input);
        _overlays.AddOverlay(_windup);
        SubscribeNetworkEvent<ArchitectArmWindupEvent>(OnWindup);
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.UseSecondary,
                new PointerInputCmdHandler(OnSecondary, false, true),
                typeof(SharedInteractionSystem))
            .Register<ArchitectArmSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<ArchitectArmSystem>();
        _overlays.RemoveOverlay(_windup);
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _windup.Enabled = _timing.CurTime < _windup.EndTime;
    }

    private void OnWindup(ArchitectArmWindupEvent ev)
    {
        _windup.StartTime = _timing.CurTime;
        _windup.EndTime = _timing.CurTime + ev.Duration;
        _windup.Enabled = true;
    }

    private bool OnSecondary(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down ||
            !_input.IsKeyDown(Keyboard.Key.Alt) ||
            _player.LocalSession?.AttachedEntity is not { } user ||
            _hands.GetActiveItem(user) is not { } item ||
            !HasComp<ArchitectArmComponent>(item))
            return false;

        RaiseNetworkEvent(new ArchitectArmDashRequestEvent(
            GetNetEntity(item),
            GetNetCoordinates(args.Coordinates)));
        return true;
    }
}
