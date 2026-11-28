// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.DeadSpace.UserInterface.Hologram.Lobby;
using Content.Client.DeadSpace.UserInterface.Hologram.Options;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

/// <summary>
/// The "Dead Space" lobby: a big title over a readiness panel (character portrait, name and wanted job) and a large
/// panel with the lobby chat and, on a second tab, the server information, on an animated backdrop. It is only a
/// second arrangement of the same controls: while the style is active they are moved into <see cref="_classicLayout"/>, and put back exactly where they were when it is switched off, so everything the lobby
/// state and controllers do with them keeps working and nothing is lost.
/// </summary>
public sealed partial class LobbyGui
{

    /// <summary>Height of the secondary lobby actions in this layout; the main "ready" action keeps its own.</summary>
    private const float ClassicActionHeight = 34f;

    private readonly List<ClassicMovedControl> _classicMoved = [];
    private HologramLobbyBackground _classicBackground = default!;
    private BoxContainer _classicLayout = default!;
    private BoxContainer _classicBody = default!;
    private Label _classicPlayersCount = default!;
    private readonly Label _classicRoundNumber = Value();
    private readonly Label _classicMode = Value();
    private readonly Label _classicMap = Value();
    private readonly Label _classicRoundPlayers = Value();
    private HologramLobbyPortrait _classicPortrait = default!;
    private Label _classicName = default!;
    private Label _classicJob = default!;

    // Slots of the hologram layout the existing controls move into.
    private BoxContainer _classicTop = default!;
    private BoxContainer _classicSubtitle = default!;
    private BoxContainer _classicPreviewRow = default!;
    private BoxContainer _classicCharacterActions = default!;
    private BoxContainer _classicStatus = default!;
    private BoxContainer _classicReady = default!;
    private BoxContainer _classicSecondary = default!;
    private BoxContainer _classicLeave = default!;
    private BoxContainer _classicServer = default!;
    private BoxContainer _classicSong = default!;
    private BoxContainer _classicNotice = default!;
    private BoxContainer _classicChat = default!;

    private bool _classicActive;
    private int _classicLastPlayers = -1;

    private void InitializeClassicLobby()
    {
        _classicBackground = new HologramLobbyBackground { Visible = false };
        AddChild(_classicBackground);
        SetAnchorPreset(_classicBackground, LayoutPreset.Wide);
        // Above the stock parallax and picture, under the interface.
        _classicBackground.SetPositionInParent(MainContainer.GetPositionInParent());

        BuildClassicLayout();
        MainContainer.AddChild(_classicLayout);

        // The portrait shows the same dummy as the stock preview, whenever that one changes.
        CharacterPreview.PreviewChanged += RefreshClassicCharacter;
    }

    /// <summary>Called every frame: follows the interface style and keeps the player count fresh.</summary>
    private void UpdateClassicLobby()
    {
        if (_classicActive)
        {
            var players = _hologramPlayers.PlayerCount;
            if (players != _classicLastPlayers)
            {
                _classicLastPlayers = players;
                _classicPlayersCount.Text = players.ToString();
            }
        }
    }

    /// <summary>Which arrangement is shown, the character editor and the hide-interface toggle included.</summary>
    private void UpdateClassicVisibility(bool art)
    {
        var setup = CharacterSetupState.Visible;
        _classicBackground.Visible = _classicActive && !art;
        _classicLayout.Visible = _classicActive && !setup;
        _classicBody.Visible = !ShowGUI.Pressed;
    }

    private void EnterClassicLayout()
    {
        // The toolbar keeps every link and admin button; vote popups stay under it.
        ClassicMove(TopPanel, _classicTop);
        ClassicMove(VoteContainer, _classicTop);
        ClassicMove(StationTime, _classicSubtitle);

        // The stock preview (full figure and description) stays behind; the portrait takes its place.
        RefreshClassicCharacter();
        ClassicMove(CharacterSetup, _classicCharacterActions, ClassicCompact);
        ClassicMove(DonateButton, _classicCharacterActions, ClassicCompact);
        ClassicMove(OptionsButton, _classicCharacterActions, ClassicCompact);

        ClassicMove(StripeBack, _classicStatus, c => c.HorizontalAlignment = HAlignment.Stretch);

        ClassicMove(ReadyButton, _classicReady);
        ClassicMove(AshWalkerCancelButton, _classicReady, ClassicCompact);
        ClassicMove(ObserveButton, _classicSecondary, c =>
        {
            ClassicCompact(c);
            c.HorizontalExpand = true;
        });
        ClassicMove(CallVoteButton, _classicSecondary, c =>
        {
            ClassicCompact(c);
            c.HorizontalExpand = true;
        });
        ClassicMove(LeaveButton, _classicLeave, ClassicCompact);

        // The playtime note concerns joining, so it sits by the ready button; the song goes under the title.
        ClassicMove(PlaytimeComment, _classicNotice);
        ClassicMove(LobbySong, _classicSong);

        ClassicMove(Chat, _classicChat);
        ApplyHologramChat();
    }

    /// <summary>The round lines, filled from the same snapshot as the cabin layout.</summary>
    private void SetClassicRound(string number, string mode, string map, string players)
    {
        _classicRoundNumber.Text = number;
        _classicMode.Text = mode;
        _classicMap.Text = map;
        _classicRoundPlayers.Text = players;
    }

    private void LeaveClassicLayout()
    {
        ResetHologramChat();

        // Back in reverse order, so every recorded index is valid again.
        for (var i = _classicMoved.Count - 1; i >= 0; i--)
        {
            var moved = _classicMoved[i];
            var control = moved.Control;
            control.Orphan();
            moved.Parent.AddChild(control);
            control.SetPositionInParent(Math.Min(moved.Index, moved.Parent.ChildCount - 1));
            control.HorizontalAlignment = moved.HorizontalAlignment;
            control.HorizontalExpand = moved.HorizontalExpand;
            control.VerticalExpand = moved.VerticalExpand;
            control.MinSize = moved.MinSize;
        }

        _classicMoved.Clear();
        _classicPortrait.SetEntity(null);
    }

    /// <summary>A secondary action: full width of its slot, a lower fixed height.</summary>
    private static void ClassicCompact(Control control)
    {
        control.MinSize = new Vector2(0, ClassicActionHeight);
    }

    /// <summary>Portrait, name and the job the selected character wants most.</summary>
    private void RefreshClassicCharacter()
    {
        if (!_classicActive)
            return;

        _classicPortrait.SetEntity(CharacterPreview.PreviewDummy);

        if (_hologramPreferences.Preferences?.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            _classicName.Text = _hologramPreferences.ServerDataLoaded
                ? string.Empty
                : Loc.GetString("lobby-character-preview-panel-unloaded-preferences-label");
            _classicJob.Text = string.Empty;
            return;
        }

        _classicName.Text = profile.Name;
        _classicJob.Text = (HologramLobbyJob.TryGetTopJob(profile, _hologramPrototypes, out var job)
                ? job.LocalizedName
                : Loc.GetString("hologram-lobby-no-job"))
            .ToUpperInvariant();
    }

    private void ClassicMove(Control control, Control slot, Action<Control>? setup = null)
    {
        if (control.Parent is not { } parent)
            return;

        _classicMoved.Add(new ClassicMovedControl(
            control,
            parent,
            control.GetPositionInParent(),
            control.HorizontalAlignment,
            control.HorizontalExpand,
            control.VerticalExpand,
            control.MinSize));

        control.Orphan();
        slot.AddChild(control);
        setup?.Invoke(control);
    }

    private void BuildClassicLayout()
    {
        _classicTop = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _classicSubtitle = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _classicPortrait = new HologramLobbyPortrait();
        // A clipped label measures zero wide: it has to stretch over the column and centre its own text.
        _classicName = new Label
        {
            StyleClasses = { HologramLobbyStyleClass.CharacterName },
            HorizontalAlignment = HAlignment.Stretch,
            Align = Label.AlignMode.Center,
            ClipText = true,
        };
        _classicJob = new Label
        {
            StyleClasses = { HologramLobbyStyleClass.Caption },
            HorizontalAlignment = HAlignment.Stretch,
            Align = Label.AlignMode.Center,
            ClipText = true,
        };
        _classicCharacterActions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        _classicPreviewRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 18,
            Children =
            {
                // Portrait in a projected frame, the name and the wanted job under it.
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 4,
                    MinWidth = 180,
                    MaxWidth = 200,
                    Children =
                    {
                        new PanelContainer
                        {
                            StyleClasses = { DeadSpaceStyleClass.Inset, HologramLobbyStyleClass.Inset },
                            HorizontalAlignment = HAlignment.Center,
                            Children = { _classicPortrait },
                        },
                        _classicName,
                        _classicJob,
                    },
                },
                _classicCharacterActions,
            },
        };
        _classicStatus = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _classicReady = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        _classicSecondary = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
        };
        _classicLeave = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _classicServer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        _classicSong = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _classicNotice = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _classicChat = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _classicPlayersCount = new Label
        {
            StyleClasses = { HologramLobbyStyleClass.Value },
            HorizontalAlignment = HAlignment.Right,
        };

        var title = new Label
        {
            Text = ClassicSpaced(Loc.GetString("hologram-lobby-title").ToUpperInvariant()),
            StyleClasses = { HologramLobbyStyleClass.BigTitle },
        };

        var ready = ClassicPanel(
            Loc.GetString("hologram-lobby-ready-panel"),
            null,
            new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 12,
                Children =
                {
                    _classicPreviewRow,
                    ClassicDivider(),
                    _classicStatus,
                    // The round, as in the cabin layout.
                    Row("hologram-lobby-round-number", _classicRoundNumber),
                    Row("hologram-lobby-round-mode", _classicMode),
                    Row("hologram-lobby-round-map", _classicMap),
                    Row("hologram-lobby-round-players", _classicRoundPlayers),
                    ClassicDivider(),
                    _classicNotice,
                    _classicReady,
                    _classicSecondary,
                    _classicLeave,
                },
            });
        ready.MinWidth = 520;

        // The big panel: the lobby chat.
        var tabs = new TabContainer
        {
            StyleClasses = { HologramMenuStyleClass.Tabs },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _classicChat },
        };
        TabContainer.SetTabTitle(_classicChat, Loc.GetString("hologram-lobby-chat-panel").ToUpperInvariant());

        var chat = ClassicPanel(null, null, tabs);
        // The same width as the chat of the cabin layout; the page shows between the columns.
        chat.MinWidth = HologramSideWidth;
        chat.VerticalExpand = true;

        _classicBody = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(36, 22, 36, 22),
                    SeparationOverride = 6,
                    Children = { title, _classicSubtitle, _classicSong },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 36,
                    Margin = new Thickness(36, 0, 36, 24),
                    Children =
                    {
                        // On a short screen the column scrolls instead of squeezing its buttons together.
                        new ScrollContainer
                        {
                            HScrollEnabled = false,
                            VerticalExpand = true,
                            MinWidth = 536,
                            Children =
                            {
                                new BoxContainer
                                {
                                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                                    VerticalExpand = true,
                                    HorizontalExpand = true,
                                    SeparationOverride = 16,
                                    Children = { ready },
                                },
                            },
                        },
                        new Control { HorizontalExpand = true },
                        chat,
                    },
                },
            },
        };

        _classicLayout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
            Children = { _classicTop, _classicBody },
        };
    }

    /// <summary>A projected panel with a header bar (title, optional muted tag) over its content; no title, no bar.</summary>
    private static PanelContainer ClassicPanel(string? title, string? tag, Control content)
    {
        if (title == null)
        {
            content.VerticalExpand = true;
            return new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.Surface, HologramLobbyStyleClass.Panel },
                Children = { content },
            };
        }

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Children =
            {
                new Label
                {
                    Text = title.ToUpperInvariant(),
                    StyleClasses = { DeadSpaceStyleClass.Title, HologramLobbyStyleClass.Title },
                    VerticalAlignment = VAlignment.Center,
                },
            },
        };

        if (tag != null)
        {
            header.AddChild(new Label
            {
                Text = tag,
                StyleClasses = { HologramLobbyStyleClass.Caption },
                VerticalAlignment = VAlignment.Center,
            });
        }

        content.VerticalExpand = true;

        return new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Surface, HologramLobbyStyleClass.Panel },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 10,
                    Children =
                    {
                        new PanelContainer
                        {
                            StyleClasses = { DeadSpaceStyleClass.SectionHeader, HologramLobbyStyleClass.Header },
                            Children = { header },
                        },
                        content,
                    },
                },
            },
        };
    }

    private static PanelContainer ClassicDivider()
    {
        return new PanelContainer { StyleClasses = { DeadSpaceStyleClass.AccentDim } };
    }

    /// <summary>Wide tracking for the big title: the engine has no letter spacing, so letters get spaces between them.</summary>
    private static string ClassicSpaced(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length * 2);
        foreach (var letter in text)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(letter);
        }

        return builder.ToString();
    }

    private readonly record struct ClassicMovedControl(
        Control Control,
        Control Parent,
        int Index,
        Control.HAlignment HorizontalAlignment,
        bool HorizontalExpand,
        bool VerticalExpand,
        Vector2 MinSize);
}
