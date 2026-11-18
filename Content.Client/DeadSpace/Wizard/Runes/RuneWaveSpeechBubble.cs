// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Content.Client.Chat.UI;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneWaveSpeechBubble : SpeechBubble
{
    public RuneWaveSpeechBubble(ChatMessage message, EntityUid senderEntity, string speechStyleClass)
        : base(message, senderEntity, speechStyleClass)
    {
    }

    private static readonly Regex WaveOpen = new(@"(?<!\\)\[" + RuneIncantation.WaveTag + @"\]", RegexOptions.Compiled);

    public static bool IsWave(ChatMessage message)
    {
        return WaveOpen.IsMatch(message.WrappedMessage);
    }

    protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
    {
        var header = new RichTextLabel
        {
            Margin = new Thickness(1, 1, 1, 1),
        };
        header.SetMessage(ExtractAndFormatSpeechSubstring(message, "BubbleHeader", fontColor));

        var content = SharedChatSystem.GetStringInsideTag(message, "BubbleContent");
        var wave = new RuneWaveLabel(FormattedMessage.RemoveMarkupPermissive(content))
        {
            Margin = new Thickness(4, 4, 4, 4),
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                new PanelContainer
                {
                    StyleClasses = { "speechBox", speechStyleClass },
                    Children = { header },
                    HorizontalAlignment = HAlignment.Center,
                    ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                },
                new PanelContainer
                {
                    StyleClasses = { "speechBox", speechStyleClass },
                    Children = { wave },
                    HorizontalAlignment = HAlignment.Center,
                    ModulateSelfOverride = Color.White.WithAlpha(ConfigManager.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)),
                },
            },
        };
    }
}

public sealed class RuneWaveLabel : Control
{
    private const float Amplitude = 4f;
    private readonly string _text;
    private readonly Font _font;
    private float _time;

    public RuneWaveLabel(string text)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (builder.Length >= 48)
                break;

            builder.Append(rune.ToString());
        }

        _text = builder.ToString();
        _font = IoCManager.Resolve<IResourceCache>().NotoStack(variation: "Bold", size: 14);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = 0f;
        foreach (var rune in _text.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        return new Vector2(MathF.Min(width, SpeechBubble.SpeechMaxWidth), _font.GetLineHeight(1f) + Amplitude * 2f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var x = 0f;
        var baseline = _font.GetAscent(scale) + Amplitude * scale;
        var index = 0;

        foreach (var rune in _text.EnumerateRunes())
        {
            var offset = MathF.Sin(_time * 6f + index * 0.55f) * Amplitude * scale;
            var hue = (_time * 0.45f + index * 0.07f) % 1f;
            var color = Color.FromHsv(new Vector4(hue, 0.8f, 1f, 1f));
            x += _font.DrawChar(handle, rune, new Vector2(x, baseline + offset), scale, color);
            index++;
        }
    }
}

public sealed class RuneWaveTag : IMarkupTagHandler
{
    public string Name => RuneIncantation.WaveTag;
}
