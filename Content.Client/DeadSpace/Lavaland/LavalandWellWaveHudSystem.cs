// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

public sealed class LavalandWellWaveHudSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private Label? _timer;
    private TimeSpan? _hideAt;

    public override void FrameUpdate(float frameTime)
    {
        if (_hideAt is { } hideAt && _timing.CurTime >= hideAt)
            Hide();
    }

    public override void Initialize()
    {
        SubscribeNetworkEvent<LavalandWellWaveTimerEvent>(OnTimer);
        SubscribeNetworkEvent<LavalandWellWaveTimerHideEvent>(_ => Hide());
    }

    private void OnTimer(LavalandWellWaveTimerEvent ev)
    {
        if (_timer == null)
        {
            _timer = new Label
            {
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Top,
            };
            _ui.WindowRoot.AddChild(_timer);
            LayoutContainer.SetAnchorPreset(_timer, LayoutContainer.LayoutPreset.CenterTop);
            LayoutContainer.SetMarginTop(_timer, 18);
        }
        _timer.Visible = true;
        _hideAt = _timing.CurTime + TimeSpan.FromSeconds(ev.Seconds + 2);
        _timer.FontColorOverride = ev.Warning ? Color.Red : Color.CornflowerBlue;
        _timer.Text = Loc.GetString(ev.Warning ? "lavaland-well-wave-timer-warning" : "lavaland-well-wave-timer-active",
            ("time", TimeSpan.FromSeconds(ev.Seconds).ToString(@"mm\:ss")));
    }

    private void Hide()
    {
        if (_timer != null)
            _timer.Visible = false;
        _hideAt = null;
    }
}
