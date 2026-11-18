// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.DebugAnalyzer;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client.DeadSpace.DebugAnalyzer;

/// <summary>
/// Shows the report of the debug analyzer, every section under its own heading.
/// </summary>
public sealed class DebugAnalyzerWindow : DefaultWindow
{
    private static readonly Color Heading = Color.FromHex("#c6e7ff");
    private static readonly Color Muted = Color.FromHex("#8a93a3");
    private static readonly Color Normal = Color.FromHex("#4cae4f");
    private static readonly Color Warning = Color.FromHex("#e3b341");
    private static readonly Color Critical = Color.FromHex("#d1453b");

    private readonly Label _targetName;
    private readonly BoxContainer _content;

    public DebugAnalyzerWindow()
    {
        MinSize = new Vector2(360, 300);
        SetSize = new Vector2(380, 520);

        _targetName = new Label
        {
            FontColorOverride = Heading,
            HorizontalAlignment = HAlignment.Center,
        };

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
            Children = { _content },
        };

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            Children = { _targetName, scroll },
        });
    }

    public void Populate(DebugAnalyzerScannedMessage message)
    {
        _targetName.Text = message.TargetName;
        _content.RemoveAllChildren();

        foreach (var section in message.Sections)
        {
            _content.AddChild(new Label
            {
                Text = Localize(section.Title),
                FontColorOverride = Heading,
                Margin = new Thickness(0, 8, 0, 2),
            });
            _content.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = Muted, ContentMarginTopOverride = 1 },
            });

            foreach (var row in section.Rows)
            {
                _content.AddChild(BuildRow(row));
            }
        }
    }

    private static Control BuildRow(DebugAnalyzerRow row)
    {
        var line = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children =
            {
                new Label
                {
                    Text = Localize(row.Label),
                    FontColorOverride = Muted,
                    HorizontalExpand = true,
                },
                new Label
                {
                    Text = row.Value,
                    FontColorOverride = row.Severity == DebugAnalyzerSeverity.Normal ? null : SeverityColor(row.Severity),
                },
            },
        };

        if (row.Fraction is not { } fraction)
            return line;

        var bar = new ProgressBar
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = fraction,
            HorizontalExpand = true,
            MinHeight = 6,
            ForegroundStyleBoxOverride = new StyleBoxFlat(SeverityColor(row.Severity)),
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 2),
            Children = { line, bar },
        };
    }

    private static Color SeverityColor(DebugAnalyzerSeverity severity)
    {
        return severity switch
        {
            DebugAnalyzerSeverity.Warning => Warning,
            DebugAnalyzerSeverity.Critical => Critical,
            _ => Normal,
        };
    }

    private static string Localize(string id)
    {
        return IoCManager.Resolve<ILocalizationManager>().TryGetString(id, out var text) ? text : id;
    }
}
