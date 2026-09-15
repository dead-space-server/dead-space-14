// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Text;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.GhostRoleIntroduction;

public sealed class GhostRoleIntroductionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private LayoutContainer? _root;
    private PanelContainer? _black;
    private Label? _operationLabel;
    private Label? _textLabel;
    private string _text = string.Empty;
    private TimeSpan _started;
    private float _duration;
    private float _fadeDuration;
    private float _fadeOutDuration;
    private float _textDelay;
    private float _charactersPerSecond;
    private Color _textColor;
    private int _visibleCharacters = -1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GhostRoleIntroductionEvent>(ShowIntroduction);
    }

    private void ShowIntroduction(GhostRoleIntroductionEvent ev)
    {
        Clear();

        var fontResource = _resourceCache.GetResource<FontResource>(new ResPath(ev.Font));
        var operationFont = new VectorFont(fontResource, Math.Max(ev.OperationFontSize, 1));
        var textFont = new VectorFont(fontResource, Math.Max(ev.FontSize, 1));
        var textWidth = Math.Clamp(_ui.WindowRoot.Size.X * 0.5f, 320f, 760f);

        _black = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.Black),
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _operationLabel = new Label
        {
            Text = WrapText(ev.OperationName, operationFont, textWidth),
            FontOverride = operationFont,
            FontColorOverride = ev.TextColor,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _textLabel = new Label
        {
            FontOverride = textFont,
            FontColorOverride = ev.TextColor,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        var textContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Center,
            SetWidth = textWidth,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        if (!string.IsNullOrWhiteSpace(ev.OperationName))
            textContainer.AddChild(_operationLabel);
        textContainer.AddChild(_textLabel);

        _root = new LayoutContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _root.AddChild(_black);
        _root.AddChild(textContainer);
        LayoutContainer.SetAnchorPreset(_black, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(textContainer, LayoutContainer.LayoutPreset.CenterLeft);
        LayoutContainer.SetMarginLeft(textContainer, 72f);

        _text = WrapText(ev.Text, textFont, textWidth);
        _textColor = ev.TextColor;
        _duration = ev.Duration;
        _fadeDuration = Math.Max(ev.FadeFromBlackDuration, 0.01f);
        _fadeOutDuration = Math.Max(ev.FadeOutDuration, 0.01f);
        _textDelay = ev.TextDelay;
        _charactersPerSecond = Math.Max(ev.CharactersPerSecond, 1f);
        _started = _timing.CurTime;

        _ui.WindowRoot.AddChild(_root);
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_root == null || _black == null || _operationLabel == null || _textLabel == null)
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
        if (_visibleCharacters != visibleCharacters)
        {
            _visibleCharacters = visibleCharacters;
            _textLabel.Text = _text[..visibleCharacters];
        }

        var textFade = Math.Clamp((_duration - elapsed) / _fadeOutDuration, 0f, 1f);
        var fadedColor = _textColor.WithAlpha(_textColor.A * textFade);
        _operationLabel.FontColorOverride = fadedColor;
        _textLabel.FontColorOverride = fadedColor;
    }

    private static string WrapText(string text, Font font, float maxWidth)
    {
        var result = new StringBuilder();
        var paragraphs = text.Replace("\r", string.Empty).Split('\n');
        var spaceWidth = MeasureText(font, " ");

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var lineWidth = 0f;
            var words = paragraphs[paragraphIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                var wordWidth = MeasureText(font, word);
                if (lineWidth > 0f && lineWidth + spaceWidth + wordWidth > maxWidth)
                {
                    result.Append('\n');
                    lineWidth = 0f;
                }

                if (lineWidth > 0f)
                {
                    result.Append(' ');
                    lineWidth += spaceWidth;
                }

                result.Append(word);
                lineWidth += wordWidth;
            }

            if (paragraphIndex < paragraphs.Length - 1)
                result.Append('\n');
        }

        return result.ToString();
    }

    private static float MeasureText(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += font.GetCharMetrics(rune, 1f)?.Advance ?? 0f;

        return width;
    }
    private void Clear()
    {
        _root?.Orphan();
        _root = null;
        _black = null;
        _operationLabel = null;
        _textLabel = null;
        _visibleCharacters = -1;
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }
}