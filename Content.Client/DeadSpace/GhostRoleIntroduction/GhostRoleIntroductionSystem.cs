// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Text;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.GhostRoleIntroduction;

public sealed class GhostRoleIntroductionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private LayoutContainer? _root;
    private PanelContainer? _black;
    private Label? _operationLabel;
    private Label? _textLabel;
    private Label? _senderLabel;

    private string _operationText = string.Empty;
    private string _text = string.Empty;
    private string _senderText = string.Empty;
    private TimeSpan _started;
    private float _duration;
    private float _fadeDuration;
    private float _fadeOutDuration;
    private float _textDelay;
    private float _charactersPerSecond;
    private Color _textColor;
    private bool _showBlackBackground;
    private bool _typeOperationName;
    private int _visibleOperationCharacters = -1;
    private int _visibleCharacters = -1;
    private float _announcementVolume = 1f;

    // Targeted tablet / war-declarator announcements share one local display.
    // If another targeted announcement reaches this same player while one is active,
    // the active text is jammed instead of stacking a second announcement over it.
    private bool _targetedAnnouncementActive;
    private bool _interferenceActive;
    private TimeSpan _interferenceEnd;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GhostRoleIntroductionEvent>(ShowIntroduction);
        Subs.CVar(_cfg, CCCCVars.AnnonceVolume, SetAnnouncementVolume, true);
    }

    private void ShowIntroduction(GhostRoleIntroductionEvent ev)
    {
        // A second targeted announcement must NOT play its own announcement sound.
        // Instead, jam the announcement that is already visible for this player.
        if (_root != null && _targetedAnnouncementActive && ev.TargetedAnnouncement)
        {
            StartInterference(ev.InterferenceSound, ev.InterferenceDuration);
            return;
        }

        Clear();

        var fontResource = _resourceCache.GetResource<FontResource>(new ResPath(ev.Font));
        var operationFont = new VectorFont(fontResource, Math.Max(ev.OperationFontSize, 1));
        var textFont = new VectorFont(fontResource, Math.Max(ev.FontSize, 1));
        var senderFont = new VectorFont(fontResource, Math.Max(ev.SenderFontSize, 1));
        var textWidth = Math.Clamp(_ui.WindowRoot.Size.X * 0.5f, 320f, 760f);

        _showBlackBackground = ev.ShowBlackBackground;
        _typeOperationName = ev.TypeOperationName;
        _targetedAnnouncementActive = ev.TargetedAnnouncement;

        _black = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(_showBlackBackground ? Color.Black : Color.Transparent),
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        _operationText = WrapText(ev.OperationName, operationFont, textWidth);
        _text = WrapText(ev.Text, textFont, textWidth);

        _operationLabel = new Label
        {
            Text = _typeOperationName ? string.Empty : _operationText,
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

        _senderText = string.IsNullOrWhiteSpace(ev.SenderJobTitle)
            ? ev.SenderName
            : $"{ev.SenderName} — {ev.SenderJobTitle}";

        _senderLabel = new Label
        {
            Text = _senderText,
            FontOverride = senderFont,
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

        if (!string.IsNullOrWhiteSpace(ev.Text))
            textContainer.AddChild(_textLabel);

        if (!string.IsNullOrWhiteSpace(_senderText))
            textContainer.AddChild(_senderLabel);

        _root = new LayoutContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        _root.AddChild(_black);
        _root.AddChild(textContainer);
        LayoutContainer.SetAnchorPreset(_black, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(textContainer, LayoutContainer.LayoutPreset.CenterLeft);
        LayoutContainer.SetMarginLeft(textContainer, 72f);

        _textColor = ev.TextColor;
        _duration = ev.Duration;
        _fadeDuration = Math.Max(ev.FadeFromBlackDuration, 0.01f);
        _fadeOutDuration = Math.Max(ev.FadeOutDuration, 0.01f);
        _textDelay = ev.TextDelay;
        _charactersPerSecond = Math.Max(ev.CharactersPerSecond, 1f);
        _started = _timing.CurTime;

        _ui.WindowRoot.AddChild(_root);
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);

        PlayLocalSound(ev.AnnouncementSound);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_root == null || _black == null || _operationLabel == null || _textLabel == null || _senderLabel == null)
            return;

        if (_interferenceActive)
        {
            if (_timing.CurTime >= _interferenceEnd)
                Clear();

            return;
        }

        var elapsed = (float) (_timing.CurTime - _started).TotalSeconds;
        if (elapsed >= _duration)
        {
            Clear();
            return;
        }

        if (_showBlackBackground)
        {
            var darkness = 1f - Math.Clamp(elapsed / _fadeDuration, 0f, 1f);
            _black.ModulateSelfOverride = Color.White.WithAlpha(darkness);
        }
        else
        {
            _black.ModulateSelfOverride = Color.Transparent;
        }

        if (_typeOperationName)
        {
            var visibleOperationCharacters = Math.Clamp(
                (int) (elapsed * _charactersPerSecond),
                0,
                _operationText.Length);

            if (_visibleOperationCharacters != visibleOperationCharacters)
            {
                _visibleOperationCharacters = visibleOperationCharacters;
                _operationLabel.Text = _operationText[..visibleOperationCharacters];
            }
        }

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
        _senderLabel.FontColorOverride = fadedColor;
    }

    private void StartInterference(SoundSpecifier? sound, float duration)
    {
        if (_root == null || _operationLabel == null || _textLabel == null || _senderLabel == null)
            return;

        _interferenceActive = true;
        _interferenceEnd = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(duration, 0.01f));

        _operationLabel.Text = MakeInterference(_operationText);
        _textLabel.Text = MakeInterference(_text);
        _senderLabel.Text = MakeInterference(_senderText);

        _operationLabel.FontColorOverride = _textColor;
        _textLabel.FontColorOverride = _textColor;
        _senderLabel.FontColorOverride = _textColor;

        PlayLocalSound(sound);
    }

    private void PlayLocalSound(SoundSpecifier? sound)
    {
        if (sound == null || _player.LocalSession?.AttachedEntity is not { Valid: true } playerEntity)
            return;

        var audioParams = sound.Params.AddVolume(SharedAudioSystem.GainToVolume(_announcementVolume));
        _audio.PlayGlobal(sound, playerEntity, audioParams);
    }

    private void SetAnnouncementVolume(float volume)
    {
        _announcementVolume = volume;
    }

    private static string MakeInterference(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        const string noise = "#@!?%/\\*+=";
        var result = new StringBuilder(source.Length);
        var noiseIndex = 0;

        foreach (var character in source)
        {
            if (character == '\r' || character == '\n' || char.IsWhiteSpace(character))
            {
                result.Append(character);
                continue;
            }

            result.Append(noise[noiseIndex % noise.Length]);
            noiseIndex++;
        }

        return result.ToString();
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
        _senderLabel = null;
        _operationText = string.Empty;
        _text = string.Empty;
        _senderText = string.Empty;
        _visibleOperationCharacters = -1;
        _visibleCharacters = -1;
        _showBlackBackground = false;
        _typeOperationName = false;
        _targetedAnnouncementActive = false;
        _interferenceActive = false;
        _interferenceEnd = default;
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }
}
