// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Necromorphs.Necroobelisk;

public sealed class SplinterBladeVisualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private PanelContainer? _darkness;
    private TimeSpan _endTime;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SplinterBladeVisualEvent>(OnVisual);
    }

    private void OnVisual(SplinterBladeVisualEvent ev)
    {
        _endTime = _timing.CurTime + TimeSpan.FromSeconds(ev.Duration);

        if (_darkness != null)
            return;

        _darkness = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.Black),
            MouseFilter = Control.MouseFilterMode.Ignore,
            ModulateSelfOverride = Color.White.WithAlpha(0.72f),
        };

        _ui.WindowRoot.AddChild(_darkness);
        LayoutContainer.SetAnchorPreset(_darkness, LayoutContainer.LayoutPreset.Wide);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_darkness == null)
            return;

        var remaining = (float) (_endTime - _timing.CurTime).TotalSeconds;
        if (remaining <= 0f)
        {
            Clear();
            return;
        }

        var fade = Math.Clamp(remaining / 1.5f, 0f, 1f);
        _darkness.ModulateSelfOverride = Color.White.WithAlpha(0.72f * fade);
    }

    private void Clear()
    {
        _darkness?.Orphan();
        _darkness = null;
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }
}
