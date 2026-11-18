// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed partial class RoundEndCreditsOverlay
{
    private const float VotePanelWidth = 960f;

    private float _votePanelWidth = VotePanelWidth;

    private BoxContainer? _votePanel;
    private CreditsProgressBar? _voteBar;
    private Label? _voteTimer;
    private int _voteSecondsShown = -1;
    private TimeSpan _voteStart;
    private TimeSpan _voteEnd;
    private int _activeNomination;
    private int[] _picks = Array.Empty<int>();
    private readonly List<VoteNominationView> _nominationViews = new();
    private readonly List<VoteCardView> _voteCards = new();
    private readonly List<Control> _voteAnimated = new();

    public void SetVote(RoundEndCreditsVoteEvent vote, IPrototypeManager prototypes)
    {
        if (_votePanel != null || _credits != null)
            return;

        _voteStart = vote.FadeEnd;
        _voteEnd = vote.VoteEnd;

        var nominations = new List<RoundEndCreditsVoteNominationPrototype>();
        foreach (var nomination in vote.Nominations)
        {
            if (prototypes.TryIndex<RoundEndCreditsVoteNominationPrototype>(nomination.Prototype, out var proto))
                nominations.Add(proto);
        }

        if (nominations.Count != vote.Nominations.Count)
            return;

        _picks = Enumerable.Range(0, nominations.Count)
            .Select(i => vote.OwnVotes.Length > i ? vote.OwnVotes[i] : -1)
            .ToArray();

        var own = vote.OwnPlayers.ToHashSet();
        var candidates = vote.Candidates
            .Where(i => i >= 0 && i < _round.AllPlayersEndInfo.Length && !own.Contains(i))
            .OrderBy(i => GetDisplayName(_round.AllPlayersEndInfo[i]), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _votePanelWidth = Math.Min(VotePanelWidth, GetCenterWidth(ScreenWidth));

        _votePanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            SetWidth = _votePanelWidth,
            MouseFilter = MouseFilterMode.Pass,
            Modulate = Color.Transparent,
        };

        _votePanel.AddChild(MakeLabel(Loc.GetString("round-end-credits-vote-title"), 34, TextColor, BoldFont));
        _votePanel.AddChild(MakeLabel(Loc.GetString("round-end-credits-vote-subtitle"), 15, DimColor));

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 16,
            HorizontalExpand = true,
        };
        body.AddChild(BuildNominationColumn(nominations));
        body.AddChild(BuildCandidateColumn(candidates));
        _votePanel.AddChild(body);

        _voteBar = new CreditsProgressBar
        {
            SetSize = new Vector2(_votePanelWidth, 4),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            FillColor = Gold,
        };
        _votePanel.AddChild(_voteBar);

        _voteTimer = MakeLabel(string.Empty, 13, DimColor);
        _votePanel.AddChild(_voteTimer);

        _activeNomination = Math.Max(0, Array.IndexOf(_picks, -1));
        RefreshVoteViews();

        AddChild(_votePanel);
        SetAnchorPreset(_votePanel, LayoutPreset.Center);
        SetGrowHorizontal(_votePanel, GrowDirection.Both);
        SetGrowVertical(_votePanel, GrowDirection.Both);
    }

    private Control BuildNominationColumn(List<RoundEndCreditsVoteNominationPrototype> nominations)
    {
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            SetWidth = _votePanelWidth >= 900f ? 300f : 220f,
        };

        for (var i = 0; i < nominations.Count; i++)
        {
            var index = i;
            var button = new ContainerButton { HorizontalExpand = true };
            var panel = new PanelContainer { HorizontalExpand = true };
            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(12, 8),
            };

            var pick = MakeLabel(string.Empty, 14, TextColor, BoldFont, center: false);
            pick.ClipText = true;

            var title = Loc.GetString(nominations[i].Name);
            box.AddChild(MakeLabel(title, 17, Gold, BoldFont, center: false));
            box.AddChild(MakeWrappedText(Loc.GetString(nominations[i].Description), 12, DimColor));
            box.AddChild(pick);
            panel.AddChild(box);
            button.AddChild(panel);
            button.OnPressed += _ =>
            {
                _activeNomination = index;
                RefreshVoteViews();
            };

            column.AddChild(button);
            _voteAnimated.Add(button);
            _nominationViews.Add(new VoteNominationView(title, button, panel, pick));
        }

        return column;
    }

    private Control BuildCandidateColumn(List<int> candidates)
    {
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        var search = new LineEdit
        {
            PlaceHolder = Loc.GetString("round-end-credits-vote-search"),
            HorizontalExpand = true,
        };
        search.OnTextChanged += args => FilterCards(args.Text);
        column.AddChild(search);

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            HorizontalExpand = true,
            SetHeight = 380,
        };

        var grid = new GridContainer
        {
            Columns = _votePanelWidth >= 900f ? 3 : 2,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true,
        };

        foreach (var candidate in candidates)
        {
            var info = _round.AllPlayersEndInfo[candidate];
            var name = GetDisplayName(info);
            var job = GetJobText(info);

            var button = new ContainerButton { HorizontalExpand = true, MinHeight = 58 };
            var panel = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };
            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(10, 6),
            };

            var nameLabel = MakeLabel(name, 15, info.Antag ? Blood : TextColor, BoldFont, center: false);
            nameLabel.ClipText = true;
            var jobLabel = MakeLabel(job, 12, DimColor, center: false);
            jobLabel.ClipText = true;
            var tags = MakeLabel(string.Empty, 11, Gold, center: false);
            tags.ClipText = true;

            box.AddChild(nameLabel);
            box.AddChild(jobLabel);
            box.AddChild(tags);
            panel.AddChild(box);
            button.AddChild(panel);
            button.OnPressed += _ => PickCandidate(candidate);

            grid.AddChild(button);
            _voteCards.Add(new VoteCardView(candidate, $"{name} {job}".ToLowerInvariant(), button, panel, tags));
        }

        if (candidates.Count == 0)
            grid.AddChild(MakeLabel(Loc.GetString("round-end-credits-vote-no-candidates"), 14, DimColor));

        scroll.AddChild(grid);
        column.AddChild(scroll);
        _voteAnimated.Add(column);
        return column;
    }

    private void FilterCards(string text)
    {
        var query = text.Trim().ToLowerInvariant();
        foreach (var card in _voteCards)
        {
            card.Button.Visible = query.Length == 0 || card.Search.Contains(query);
        }
    }

    private void PickCandidate(int candidate)
    {
        if (_activeNomination < 0 || _activeNomination >= _picks.Length)
            return;

        var nomination = _activeNomination;
        _picks[nomination] = _picks[nomination] == candidate ? -1 : candidate;
        VoteCast?.Invoke(nomination, _picks[nomination]);

        if (_picks[nomination] != -1)
        {
            for (var step = 1; step < _picks.Length; step++)
            {
                var next = (nomination + step) % _picks.Length;
                if (_picks[next] != -1)
                    continue;

                _activeNomination = next;
                break;
            }
        }

        RefreshVoteViews();
    }

    private void RefreshVoteViews()
    {
        for (var i = 0; i < _nominationViews.Count; i++)
        {
            var view = _nominationViews[i];
            var pick = _picks[i];
            view.Pick.Text = pick >= 0 && TryGetPlayer(pick, out var info)
                ? Loc.GetString("round-end-credits-vote-picked", ("name", GetDisplayName(info)))
                : Loc.GetString("round-end-credits-vote-not-picked");
            view.Pick.FontColorOverride = pick >= 0 ? TextColor : DimColor;
        }

        foreach (var card in _voteCards)
        {
            var titles = new List<string>();
            for (var i = 0; i < _picks.Length; i++)
            {
                if (_picks[i] == card.Player)
                    titles.Add(_nominationViews[i].Name);
            }

            card.Tags.Text = titles.Count == 0
                ? string.Empty
                : Loc.GetString("round-end-credits-vote-card-tags", ("nominations", string.Join(", ", titles)));
        }

        UpdateVoteHighlights();
    }

    private void UpdateVoteHighlights()
    {
        for (var i = 0; i < _nominationViews.Count; i++)
        {
            var view = _nominationViews[i];
            var active = i == _activeNomination;
            SetCardStyle(view.Panel, active ? CardState.Selected : CardState.Normal, active || view.Button.IsHovered);
        }

        var activePick = _activeNomination >= 0 && _activeNomination < _picks.Length ? _picks[_activeNomination] : -1;
        foreach (var card in _voteCards)
        {
            var state = card.Player == activePick
                ? CardState.Selected
                : _picks.Contains(card.Player) ? CardState.Picked : CardState.Normal;
            SetCardStyle(card.Panel, state, card.Button.IsHovered);
        }
    }

    private static StyleBoxFlat[]? _cardStyles;

    private static StyleBoxFlat[] CreateCardStyles()
    {
        var borders = new[] { Color.White.WithAlpha(0.08f), Gold.WithAlpha(0.35f), Gold };
        var styles = new StyleBoxFlat[borders.Length * 2];
        for (var state = 0; state < borders.Length; state++)
        {
            for (var hover = 0; hover < 2; hover++)
            {
                styles[state * 2 + hover] = new StyleBoxFlat
                {
                    BackgroundColor = (hover == 1 ? CardHover : CardBackground).WithAlpha(0.95f),
                    BorderColor = borders[state],
                    BorderThickness = new Thickness(state == (int) CardState.Selected ? 2 : 1),
                };
            }
        }

        return styles;
    }

    private static void SetCardStyle(PanelContainer panel, CardState state, bool hovered)
    {
        _cardStyles ??= CreateCardStyles();
        var style = _cardStyles[(int) state * 2 + (hovered ? 1 : 0)];
        if (!ReferenceEquals(panel.PanelOverride, style))
            panel.PanelOverride = style;
    }

    private enum CardState
    {
        Normal,
        Picked,
        Selected,
    }

    private static RichTextLabel MakeWrappedText(string text, int size, Color color)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        label.SetMarkup($"[font size={size}][color={color.ToHexNoAlpha()}]{FormattedMessage.EscapeText(text)}[/color][/font]");
        return label;
    }

    private void UpdateVote(TimeSpan now)
    {
        if (_votePanel == null)
            return;

        var sinceStart = (float) (now - _voteStart).TotalSeconds;
        var untilEnd = (float) (_voteEnd - now).TotalSeconds;

        if (untilEnd <= -PanelFadeSeconds)
        {
            _votePanel.Orphan();
            _votePanel = null;
            return;
        }

        var alpha = Math.Clamp(Math.Min(sinceStart, untilEnd + PanelFadeSeconds) / PanelFadeSeconds, 0f, 1f);
        _votePanel.Modulate = Color.White.WithAlpha(alpha);

        for (var i = 0; i < _voteAnimated.Count; i++)
        {
            var rowAlpha = Math.Clamp((sinceStart - 0.25f - i * 0.15f) / PanelFadeSeconds, 0f, 1f);
            _voteAnimated[i].Modulate = Color.White.WithAlpha(rowAlpha);
        }

        UpdateVoteHighlights();

        var total = Math.Max(0.01f, (float) (_voteEnd - _voteStart).TotalSeconds);
        var left = Math.Clamp(untilEnd, 0f, total);

        if (_voteBar != null)
            _voteBar.Value = left / total;

        var seconds = (int) MathF.Ceiling(left);
        if (_voteTimer != null && seconds != _voteSecondsShown)
        {
            _voteSecondsShown = seconds;
            _voteTimer.Text = Loc.GetString("round-end-credits-vote-time", ("seconds", seconds));
        }
    }

    private sealed record VoteNominationView(string Name, ContainerButton Button, PanelContainer Panel, Label Pick);

    private sealed record VoteCardView(int Player, string Search, ContainerButton Button, PanelContainer Panel, Label Tags);
}
