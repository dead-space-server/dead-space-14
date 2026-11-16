// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Pointing;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using Content.Shared.Pointing;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Pointing;

public sealed partial class PointingBubbleSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly List<PointingBubble> _bubbles = new();
    private PointingBubbleOverlay? _overlay;
    private bool _showBubbles = true;

    public IReadOnlyList<PointingBubble> Bubbles => _bubbles;
    public bool ShowBubbles => _showBubbles;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PointingBubbleEvent>(OnBubble);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => _bubbles.Clear());
        _input.UIKeyBindStateChanged += OnUiKeyBind;

        _overlay = new PointingBubbleOverlay(this);
        _overlays.AddOverlay(_overlay);
        Subs.CVar(_cfg, CCCCVars.PointingBubbles, OnBubblesToggled, true);
    }

    public override void Shutdown()
    {
        _input.UIKeyBindStateChanged -= OnUiKeyBind;
        _overlays.RemoveOverlay<PointingBubbleOverlay>();
        _overlay?.Dispose();
        _overlay = null;
        _bubbles.Clear();

        base.Shutdown();
    }

    public void Cull()
    {
        var now = _timing.CurTime;
        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            if (_bubbles[i].End <= now)
                _bubbles.RemoveAt(i);
        }
    }

    private void OnBubblesToggled(bool enabled)
    {
        _showBubbles = enabled;
        if (!enabled)
            _bubbles.Clear();
    }

    private void OnBubble(PointingBubbleEvent ev)
    {
        if (!_showBubbles)
            return;

        var bubble = new PointingBubble(ev.Pointer, ev.Target, _timing.CurTime, ev.EndTime);
        for (var i = 0; i < _bubbles.Count; i++)
        {
            if (_bubbles[i].Pointer != ev.Pointer)
                continue;

            _bubbles[i] = bubble;
            return;
        }

        _bubbles.Add(bubble);
    }

    private bool OnUiKeyBind(BoundKeyEventArgs args)
    {
        if (args.Function != ContentKeyFunctions.Point || args.State != BoundKeyState.Down || args.Handled)
            return false;

        var control = _ui.MouseGetControl(args.PointerLocation);
        if (control == null || OverViewport(control))
            return false;

        if (TryUiEntity(control, out var uid))
            RaiseNetworkEvent(new PointingAttemptEvent(GetNetEntity(uid)));

        args.Handle();
        return true;
    }

    private bool TryUiEntity(Control? control, out EntityUid uid)
    {
        for (; control != null; control = control.Parent)
        {
            if (control is not IEntityControl entityControl
                || entityControl.UiEntity is not { } entity
                || !Exists(entity)
                || IsClientSide(entity))
                continue;

            uid = entity;
            return true;
        }

        uid = default;
        return false;
    }

    private static bool OverViewport(Control start)
    {
        for (Control? control = start; control != null; control = control.Parent)
        {
            if (control is IViewportControl)
                return true;
        }

        return false;
    }
}

public readonly record struct PointingBubble(NetEntity Pointer, NetEntity Target, TimeSpan Start, TimeSpan End);
