// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.GhostRoleIntroduction;

public sealed class GhostRoleIntroductionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private LayoutContainer? _root;
    private PanelContainer? _black;
    private RichTextLabel? _label;
    private string _text = string.Empty;
    private TimeSpan _started;
    private float _duration;
    private float _fadeDuration;
    private float _textDelay;
    private float _charactersPerSecond;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GhostRoleIntroductionEvent>(ShowIntroduction);
    }

    private void ShowIntroduction(GhostRoleIntroductionEvent ev)
    {
        Clear();

        _black = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.Black),
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _label = new RichTextLabel
        {
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Center,
            SetWidth = 620f,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        _root = new LayoutContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _root.AddChild(_black);
        _root.AddChild(_label);
        LayoutContainer.SetAnchorPreset(_black, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(_label, LayoutContainer.LayoutPreset.CenterLeft);
        LayoutContainer.SetMarginLeft(_label, 72f);

        _text = ev.Text;
        _duration = ev.Duration;
        _fadeDuration = Math.Max(ev.FadeFromBlackDuration, 0.01f);
        _textDelay = ev.TextDelay;
        _charactersPerSecond = Math.Max(ev.CharactersPerSecond, 1f);
        _started = _timing.CurTime;

        _ui.WindowRoot.AddChild(_root);
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_root == null || _black == null || _label == null)
            return;

        var elapsed = (float) (_timing.CurTime - _started).TotalSeconds;
        if (elapsed >= _duration)
        {
            Clear();
            return;
        }

        var darkness = 1f - Math.Clamp(elapsed / _fadeDuration, 0f, 1f);
        _black.ModulateSelfOverride = Color.White.WithAlpha(darkness);

        var visibleCharacters = Math.Clamp(
            (int) ((elapsed - _textDelay) * _charactersPerSecond),
            0,
            _text.Length);
        var visibleText = _text[..visibleCharacters];
        var textFade = Math.Clamp((_duration - elapsed) / 1.5f, 0f, 1f);
        _label.ModulateSelfOverride = Color.White.WithAlpha(textFade);
        _label.SetMessage(FormattedMessage.FromMarkupPermissive(visibleText));
    }

    private void Clear()
    {
        _root?.Orphan();
        _root = null;
        _black = null;
        _label = null;
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }
}