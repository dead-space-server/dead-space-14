// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed partial class RoundEndCreditsOverlay
{
    private const int CastColumns = 3;
    private const float HeadlineCharsPerSecond = 28f;

    private BoxContainer? _intro;
    private Label? _introHeadline;
    private string _introHeadlineText = string.Empty;
    private Color _introHeadlineColor;
    private IPrototypeManager? _prototypes;

    public void SetPrototypes(IPrototypeManager prototypes)
    {
        _prototypes = prototypes;
    }

    private void BuildIntro()
    {
        if (_credits == null)
            return;

        var outcome = _credits.Outcome;
        (_introHeadlineText, _introHeadlineColor) = GetHeadline(outcome);

        _intro = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            SetWidth = GetCenterWidth(ScreenWidth),
            MouseFilter = MouseFilterMode.Ignore,
            Modulate = Color.Transparent,
        };

        _intro.AddChild(MakeLabel(Loc.GetString("round-end-credits-studio"), 14, DimColor, BoldFont));
        _intro.AddChild(MakeLabel(string.IsNullOrWhiteSpace(_round.GamemodeTitle)
            ? Loc.GetString("round-end-credits-round-no-mode", ("roundId", _round.RoundId))
            : Loc.GetString("round-end-credits-round", ("roundId", _round.RoundId), ("gamemode", _round.GamemodeTitle)), 20, TextColor));

        _introHeadline = MakeLabel(string.Empty, 46, _introHeadlineColor, BoldFont);
        _introHeadline.Margin = new Thickness(0, 18, 0, 6);
        _intro.AddChild(_introHeadline);

        var sub = GetHeadlineSubtitle(outcome);
        if (sub != null)
            _intro.AddChild(MakeLabel(sub, 16, DimColor));

        if (outcome.CrewTotal > 0)
        {
            _intro.AddChild(MakeLabel(Loc.GetString("round-end-credits-crew-summary",
                ("escaped", outcome.CrewEscaped),
                ("alive", outcome.CrewAlive),
                ("dead", outcome.CrewDead),
                ("total", outcome.CrewTotal)), 16, TextColor));
        }

        var duration = _round.RoundDuration;
        _intro.AddChild(MakeLabel(Loc.GetString("round-end-credits-duration",
            ("time", $"{(int) duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}")), 14, DimColor));

        AddChild(_intro);
        SetAnchorPreset(_intro, LayoutPreset.Center);
        SetGrowHorizontal(_intro, GrowDirection.Both);
        SetGrowVertical(_intro, GrowDirection.Both);
    }

    private void UpdateIntro(float time)
    {
        if (_intro == null)
            return;

        if (time >= IntroSeconds)
        {
            _intro.Orphan();
            _intro = null;
            return;
        }

        var alpha = Math.Clamp(Math.Min(time / 0.9f, (IntroSeconds - time) / 1.1f), 0f, 1f);
        _intro.Modulate = Color.White.WithAlpha(alpha);

        if (_introHeadline == null)
            return;

        var visible = Math.Clamp((int) ((time - 1f) * HeadlineCharsPerSecond), 0, _introHeadlineText.Length);
        var text = _introHeadlineText[..visible];
        if (visible < _introHeadlineText.Length && (int) (time * 6f) % 2 == 0)
            text += "_";

        _introHeadline.Text = text;

        var pulse = 0.82f + 0.18f * MathF.Sin(time * 3f);
        _introHeadline.FontColorOverride = _introHeadlineColor.WithAlpha(pulse);
    }

    private (string Text, Color Color) GetHeadline(RoundEndCreditsOutcome outcome)
    {
        var team = LocalizeOrRaw(outcome.HeadlineTeam ?? string.Empty);
        return outcome.Headline switch
        {
            RoundEndCreditsHeadline.CrewVictory => (Loc.GetString("round-end-credits-headline-crew-victory"), Victory),
            RoundEndCreditsHeadline.AntagVictory => (Loc.GetString("round-end-credits-headline-antag-victory", ("team", team)), Blood),
            RoundEndCreditsHeadline.AntagPartialVictory => (Loc.GetString("round-end-credits-headline-antag-partial", ("team", team)), Partial),
            RoundEndCreditsHeadline.Calm => (Loc.GetString("round-end-credits-headline-calm"), Cyan),
            RoundEndCreditsHeadline.Massacre => (Loc.GetString("round-end-credits-headline-massacre"), Blood),
            _ => (Loc.GetString("round-end-credits-headline-undetermined"), TextColor),
        };
    }

    private static string? GetHeadlineSubtitle(RoundEndCreditsOutcome outcome)
    {
        return outcome.Headline switch
        {
            RoundEndCreditsHeadline.Calm => Loc.GetString("round-end-credits-headline-calm-sub"),
            RoundEndCreditsHeadline.Massacre => Loc.GetString("round-end-credits-headline-massacre-sub",
                ("dead", outcome.TotalDeaths)),
            RoundEndCreditsHeadline.CrewVictory => Loc.GetString("round-end-credits-headline-crew-victory-sub"),
            _ => null,
        };
    }

    private BoxContainer BuildContent()
    {
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 18,
            MouseFilter = MouseFilterMode.Ignore,
        };

        if (_credits == null)
            return content;

        AddOutcome(content, _credits.Outcome);
        AddMvp(content, _credits.Mvp, Loc.GetString("round-end-credits-mvp-title"), Gold);
        AddMvp(content, _credits.AntagMvp, Loc.GetString("round-end-credits-antag-mvp-title"), Blood);
        AddAwards(content, Loc.GetString("round-end-credits-section-honors"), Gold,
            _credits.Honors.Select(a => (a.Prototype, a.Player, a.Value)));
        AddAntags(content);
        AddAwards(content, Loc.GetString("round-end-credits-section-nominations"), Partial,
            _credits.Nominations.Select(a => (a.Prototype, a.Player, a.Value)));
        AddVotes(content);
        AddArena(content);
        AddCast(content);
        AddOutro(content);

        return content;
    }

    private void AddSectionTitle(BoxContainer content, string title, Color color)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 60, 0, 6),
            MouseFilter = MouseFilterMode.Ignore,
        };
        box.AddChild(MakeLabel(title.ToUpperInvariant(), 26, color, BoldFont));
        box.AddChild(new PanelContainer
        {
            SetSize = new Vector2(120, 2),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            PanelOverride = new StyleBoxFlat(color.WithAlpha(0.6f)),
        });
        content.AddChild(box);
    }

    private void AddOutcome(BoxContainer content, RoundEndCreditsOutcome outcome)
    {
        AddSectionTitle(content, Loc.GetString("round-end-credits-section-outcome"), TextColor);

        foreach (var team in outcome.Teams)
        {
            var (result, color) = GetTeamResult(team.Result);
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                MouseFilter = MouseFilterMode.Ignore,
            };
            row.AddChild(MakeLabel(Loc.GetString("round-end-credits-team",
                ("team", LocalizeOrRaw(team.Name)),
                ("members", team.Members)), 20, TextColor, BoldFont));
            row.AddChild(MakeLabel(result, 18, color, BoldFont));
            row.AddChild(MakeLabel(team.TotalObjectives > 0
                ? Loc.GetString("round-end-credits-team-details",
                    ("completed", team.CompletedObjectives),
                    ("total", team.TotalObjectives),
                    ("survivors", team.Survivors),
                    ("members", team.Members))
                : Loc.GetString("round-end-credits-team-details-no-objectives",
                    ("survivors", team.Survivors),
                    ("members", team.Members)), 14, DimColor));
            content.AddChild(row);
        }

        if (outcome.Teams.Count == 0 && _credits?.Statuses.Length > 0)
            content.AddChild(MakeLabel(Loc.GetString("round-end-credits-no-antags"), 16, DimColor));

        var report = GetReportText(_round.RoundEndText);
        if (report == null)
            return;

        content.AddChild(MakeLabel(Loc.GetString("round-end-credits-section-report"), 16, DimColor, BoldFont));
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            Margin = new Thickness(40, 0),
        };
        label.SetMarkupPermissive(report);
        content.AddChild(label);
    }

    private static (string Text, Color Color) GetTeamResult(RoundEndCreditsTeamResult result)
    {
        return result switch
        {
            RoundEndCreditsTeamResult.MajorVictory => (Loc.GetString("round-end-credits-team-major-victory"), Blood),
            RoundEndCreditsTeamResult.MinorVictory => (Loc.GetString("round-end-credits-team-minor-victory"), Partial),
            RoundEndCreditsTeamResult.MinorDefeat => (Loc.GetString("round-end-credits-team-minor-defeat"), Gold),
            RoundEndCreditsTeamResult.MajorDefeat => (Loc.GetString("round-end-credits-team-major-defeat"), Victory),
            _ => (Loc.GetString("round-end-credits-team-unknown"), DimColor),
        };
    }

    private void AddMvp(BoxContainer content, RoundEndCreditsMvp? mvp, string title, Color color)
    {
        if (mvp is not { } award || !TryGetPlayer(award.Player, out var info))
            return;

        AddSectionTitle(content, title, color);

        var card = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var doll = MakeDoll(info.DollData, 192, 4f, spin: true, framed: color, dead: GetStatus(award.Player) == RoundEndCreditsPlayerStatus.Dead);
        if (doll != null)
            card.AddChild(doll);

        card.AddChild(MakeLabel(GetDisplayName(info), 34, TextColor, BoldFont));
        card.AddChild(MakeLabel(GetJobText(info), 16, DimColor));
        card.AddChild(MakeLabel(GetCkeyText(info), 14, Cyan));
        card.AddChild(MakeLabel(Loc.GetString($"round-end-credits-stat-{award.Reason.ToString().ToLowerInvariant()}",
            ("value", (int) MathF.Round(award.Value))), 20, color));
        content.AddChild(card);
    }

    private void AddAwards(
        BoxContainer content,
        string title,
        Color color,
        IEnumerable<(string Prototype, int Player, float Value)> awards)
    {
        var list = awards.ToList();
        if (list.Count == 0 || _prototypes == null)
            return;

        AddSectionTitle(content, title, color);

        foreach (var (prototype, player, value) in list)
        {
            if (!_prototypes.TryIndex<RoundEndCreditsNominationPrototype>(prototype, out var proto) ||
                !TryGetPlayer(player, out var info))
            {
                continue;
            }

            content.AddChild(MakeAwardRow(
                Loc.GetString(proto.Name),
                info,
                Loc.GetString(proto.Description, ("value", (int) value)),
                color));
        }
    }

    private void AddVotes(BoxContainer content)
    {
        if (_credits == null || _credits.Votes.Count == 0 || _prototypes == null)
            return;

        AddSectionTitle(content, Loc.GetString("round-end-credits-section-votes"), Cyan);

        foreach (var vote in _credits.Votes)
        {
            if (!_prototypes.TryIndex<RoundEndCreditsVoteNominationPrototype>(vote.Prototype, out var proto))
                continue;

            if (!TryGetPlayer(vote.Player, out var info))
            {
                var empty = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    MouseFilter = MouseFilterMode.Ignore,
                };
                empty.AddChild(MakeLabel(Loc.GetString(proto.Name), 20, Cyan, BoldFont));
                empty.AddChild(MakeLabel(Loc.GetString("round-end-credits-vote-nobody"), 14, DimColor));
                content.AddChild(empty);
                continue;
            }

            content.AddChild(MakeAwardRow(
                Loc.GetString(proto.Name),
                info,
                Loc.GetString("round-end-credits-vote-votes", ("votes", vote.Votes)),
                Cyan));
        }
    }

    private Control MakeAwardRow(string title, RoundEndMessageEvent.RoundEndPlayerInfo info, string description, Color color)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 18,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var doll = MakeDoll(info.DollData, 72, 2f, spin: false, framed: null, dead: false);
        if (doll != null)
            row.AddChild(doll);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 420,
            MouseFilter = MouseFilterMode.Ignore,
        };
        text.AddChild(MakeLabel(title, 20, color, BoldFont, center: false));
        text.AddChild(MakeLabel($"{GetDisplayName(info)} · {GetCkeyText(info)}", 16, TextColor, center: false));
        text.AddChild(MakeLabel(description, 13, DimColor, ItalicFont, center: false));
        row.AddChild(text);

        return row;
    }

    private void AddAntags(BoxContainer content)
    {
        var antags = Enumerable.Range(0, _round.AllPlayersEndInfo.Length)
            .Where(i => _round.AllPlayersEndInfo[i].Antag && _round.AllPlayersEndInfo[i].ShowInAntagManifest)
            .OrderByDescending(i => _round.AllPlayersEndInfo[i].ManifestKills)
            .ThenBy(i => GetDisplayName(_round.AllPlayersEndInfo[i]), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (antags.Count == 0)
            return;

        AddSectionTitle(content, Loc.GetString("round-end-credits-section-antags"), Blood);

        foreach (var index in antags)
        {
            content.AddChild(MakeAntagCard(index, _round.AllPlayersEndInfo[index]));
        }
    }

    private Control MakeAntagCard(int index, RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        var card = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 20,
            HorizontalExpand = true,
            Margin = new Thickness(40, 0),
            MouseFilter = MouseFilterMode.Ignore,
        };

        var status = GetStatus(index);
        var doll = MakeDoll(info.DollData, 128, 3f, spin: false, framed: Blood, dead: status == RoundEndCreditsPlayerStatus.Dead);
        if (doll != null)
            card.AddChild(doll);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        text.AddChild(MakeLabel(GetDisplayName(info), 24, TextColor, BoldFont, center: false));
        text.AddChild(MakeLabel(string.Join(", ", (info.AntagRoleNames ?? Array.Empty<string>()).Select(LocalizeOrRaw)), 15, Blood, center: false));
        text.AddChild(MakeLabel($"{GetCkeyText(info)} · {GetStatusText(index, info)}", 13, DimColor, center: false));
        text.AddChild(MakeLabel(Loc.GetString("round-end-credits-kills-assists",
            ("kills", info.ManifestKills),
            ("assists", info.ManifestAssists)), 13, DimColor, center: false));

        foreach (var objective in info.ManifestObjectives ?? Array.Empty<RoundEndMessageEvent.RoundEndObjectiveInfo>())
        {
            var (statusText, color) = GetObjectiveStatus(objective.Progress);
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 12,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            var title = new RichTextLabel { HorizontalExpand = true, MouseFilter = MouseFilterMode.Ignore };
            title.SetMessage(FormattedMessage.FromUnformatted(objective.Title), TextColor);
            row.AddChild(title);
            row.AddChild(new Label
            {
                Text = statusText,
                FontColorOverride = color,
                FontOverride = GetFont(13, BoldFont),
                VerticalAlignment = VAlignment.Top,
            });
            text.AddChild(row);
        }

        text.AddChild(MakeQuote(info.ManifestQuote));
        card.AddChild(text);
        return card;
    }

    private static (string Text, Color Color) GetObjectiveStatus(float progress)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        var percent = Math.Round(clamped * 100f);

        if (clamped > 0.99f)
            return (Loc.GetString("round-end-summary-window-objectives-status-success", ("progress", percent)), Victory);

        if (clamped >= 0.5f)
            return (Loc.GetString("round-end-summary-window-objectives-status-partial-success", ("progress", percent)), Gold);

        if (clamped > 0f)
            return (Loc.GetString("round-end-summary-window-objectives-status-partial-failure", ("progress", percent)), Partial);

        return (Loc.GetString("round-end-summary-window-objectives-status-failure", ("progress", percent)), Blood);
    }

    private void AddArena(BoxContainer content)
    {
        if (_arena == null || _arena.Players.Count == 0)
            return;

        AddSectionTitle(content, Loc.GetString("round-end-credits-section-arena"), Partial);

        var place = 0;
        foreach (var player in _arena.Players.Take(5))
        {
            place++;
            content.AddChild(MakeLabel(Loc.GetString("round-end-credits-arena-row",
                ("place", place),
                ("name", player.PlayerName),
                ("kills", player.Kills),
                ("deaths", player.Deaths)), place == 1 ? 20 : 16, place == 1 ? Gold : TextColor, place == 1 ? BoldFont : RegularFont));
        }
    }

    private void AddCast(BoxContainer content)
    {
        var cast = Enumerable.Range(0, _round.AllPlayersEndInfo.Length)
            .Where(i => GetStatus(i) != RoundEndCreditsPlayerStatus.Observer && !IsPureObserver(_round.AllPlayersEndInfo[i]))
            .OrderBy(i => GetJobText(_round.AllPlayersEndInfo[i]), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => GetDisplayName(_round.AllPlayersEndInfo[i]), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (cast.Count == 0)
            return;

        AddSectionTitle(content, Loc.GetString("round-end-credits-section-cast"), TextColor);

        for (var start = 0; start < cast.Count; start += CastColumns)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 12,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
            };

            for (var i = start; i < Math.Min(start + CastColumns, cast.Count); i++)
            {
                row.AddChild(MakeCastCard(cast[i], _round.AllPlayersEndInfo[cast[i]]));
            }

            for (var i = cast.Count; i < start + CastColumns; i++)
            {
                row.AddChild(new Control { HorizontalExpand = true });
            }

            content.AddChild(row);
        }
    }

    private Control MakeCastCard(int index, RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        var card = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var status = GetStatus(index);
        var doll = MakeDoll(info.DollData, 72, 2f, spin: false, framed: null, dead: status == RoundEndCreditsPlayerStatus.Dead);
        if (doll != null)
            card.AddChild(doll);

        card.AddChild(MakeLabel(GetDisplayName(info), 16, info.Antag ? Blood : TextColor, BoldFont));
        card.AddChild(MakeLabel(GetJobText(info), 12, DimColor));
        card.AddChild(MakeLabel(GetCkeyText(info), 12, Cyan));
        card.AddChild(MakeLabel(GetStatusText(index, info), 12, GetStatusColor(index, info)));

        var quote = MakeQuote(info.ManifestQuote);
        quote.Margin = new Thickness(8, 4, 8, 0);
        card.AddChild(quote);

        return card;
    }

    private Control MakeQuote(string? quote)
    {
        var text = string.IsNullOrWhiteSpace(quote)
            ? Loc.GetString("round-end-summary-window-antag-manifest-quote-fallback")
            : quote;

        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        var escaped = FormattedMessage.EscapeText(Loc.GetString("round-end-credits-quote", ("quote", text)));
        label.SetMarkup($"[font size=12][italic][color={DimColor.ToHexNoAlpha()}]{escaped}[/color][/italic][/font]");
        return label;
    }

    private void AddOutro(BoxContainer content)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(0, 120, 0, 40),
            MouseFilter = MouseFilterMode.Ignore,
        };
        box.AddChild(MakeLabel(Loc.GetString("round-end-credits-outro-title"), 30, TextColor, BoldFont));
        box.AddChild(MakeLabel(Loc.GetString("round-end-credits-studio"), 16, DimColor, BoldFont));
        content.AddChild(box);
    }

    private Control? MakeDoll(RoundEndDollData? data, float size, float scale, bool spin, Color? framed, bool dead)
    {
        if (data == null)
            return null;

        var panel = new PanelContainer
        {
            SetSize = new Vector2(size, size),
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.White.WithAlpha(0.03f),
                BorderColor = (framed ?? Color.White).WithAlpha(framed != null ? 0.55f : 0.08f),
                BorderThickness = new Thickness(framed != null ? 2 : 1),
            },
        };

        var view = new RoundEndCreditsDollView(data, _dollPreviews, _entMan)
        {
            SetSize = new Vector2(size - 4, size - 4),
            Scale = new Vector2(scale, scale),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Spin = spin,
            Modulate = dead ? Color.FromHex("#7a7a7a") : Color.White,
        };
        view.Request(_dollOwner);
        _dolls.Add(view);
        panel.AddChild(view);

        return panel;
    }

    private bool TryGetPlayer(int index, out RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (index >= 0 && index < _round.AllPlayersEndInfo.Length)
        {
            info = _round.AllPlayersEndInfo[index];
            return true;
        }

        info = default;
        return false;
    }

    private RoundEndCreditsPlayerStatus? GetStatus(int index)
    {
        if (_credits == null || index < 0 || index >= _credits.Statuses.Length)
            return null;

        return _credits.Statuses[index];
    }

    private string GetStatusText(int index, RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (info.InCustody)
            return Loc.GetString("round-end-credits-status-custody");

        return GetStatus(index) switch
        {
            RoundEndCreditsPlayerStatus.Escaped => Loc.GetString("round-end-credits-status-escaped"),
            RoundEndCreditsPlayerStatus.Alive => Loc.GetString("round-end-credits-status-alive"),
            RoundEndCreditsPlayerStatus.Dead => Loc.GetString("round-end-credits-status-dead"),
            RoundEndCreditsPlayerStatus.Observer => Loc.GetString("round-end-credits-status-observer"),
            _ => string.Empty,
        };
    }

    private Color GetStatusColor(int index, RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (info.InCustody)
            return Partial;

        return GetStatus(index) switch
        {
            RoundEndCreditsPlayerStatus.Escaped => Victory,
            RoundEndCreditsPlayerStatus.Dead => Blood,
            _ => DimColor,
        };
    }

    private static bool IsPureObserver(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        return info.Observer && (info.JobRoleNames?.Length ?? 0) == 0 && (info.AntagRoleNames?.Length ?? 0) == 0;
    }

    private static string GetDisplayName(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.PlayerPseudonym))
            return info.PlayerPseudonym;

        return string.IsNullOrWhiteSpace(info.PlayerICName)
            ? Loc.GetString("generic-unknown-title")
            : info.PlayerICName;
    }

    private string GetJobText(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (IsPureObserver(info))
            return Loc.GetString("round-end-credits-status-observer");

        var roles = info.JobRoleNames ?? Array.Empty<string>();
        if (roles.Length == 0)
            roles = info.AntagRoleNames ?? Array.Empty<string>();

        if (roles.Length == 0 && !string.IsNullOrWhiteSpace(info.Role))
            roles = new[] { info.Role };

        return roles.Length == 0
            ? Loc.GetString("game-ticker-unknown-role")
            : string.Join(", ", roles.Select(LocalizeOrRaw));
    }

    private static string GetCkeyText(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        return Loc.GetString("round-end-credits-ckey", ("ckey", info.PlayerOOCName));
    }

    private static string? GetReportText(string roundEnd)
    {
        if (string.IsNullOrWhiteSpace(roundEnd))
            return null;

        var lines = roundEnd
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    private string LocalizeOrRaw(string value)
    {
        return _loc.TryGetString(value, out var localized) ? localized : value;
    }
}
