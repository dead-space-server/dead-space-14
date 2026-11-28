// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.DeadSpace.UserInterface.Hologram.Lobby;
using Content.Client.DeadSpace.UserInterface.Hologram.Options;
using Content.Client.Lobby;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Lobby;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

/// <summary>
/// The "Dead Space" lobby, "shuttle cabin": the toolbar on top; on the left the round and readiness panel; on the right
/// the lobby chat with the server information on a second tab; between them the title and the cabin scene with the
/// player's character in a seat. It is only a second arrangement of the same controls: while the style is active they
/// are moved into <see cref="_hologramLayout"/>, and put back exactly where they were when it is switched off, so
/// everything the lobby state and controllers do with them keeps working and nothing is lost.
/// </summary>
public sealed partial class LobbyGui
{
    [Dependency] private IPlayerManager _hologramPlayers = default!;
    [Dependency] private IClientPreferencesManager _hologramPreferences = default!;
    [Dependency] private IPrototypeManager _hologramPrototypes = default!;
    [Dependency] private IEntityManager _hologramEntities = default!;
    [Dependency] private IConfigurationManager _hologramCfg = default!;

    /// <summary>Height of the secondary lobby actions in this layout; the main "ready" action keeps its own.</summary>
    private const float HologramActionHeight = 34f;

    private const float HologramSideWidth = 420f;

    private readonly List<HologramMovedControl> _hologramMoved = [];
    private HologramPage _hologramPage = default!;
    private BoxContainer _hologramLayout = default!;
    private BoxContainer _hologramBody = default!;
    private HologramShuttleCabin _hologramCabin = default!;
    private HologramLobbyArt _hologramArt = default!;

    private Label _hologramRoundNumber = default!;
    private Label _hologramMode = default!;
    private Label _hologramMap = default!;
    private Label _hologramPlayersValue = default!;
    private Label _hologramCharacter = default!;

    // Slots of the hologram layout the existing controls move into.
    private BoxContainer _hologramTop = default!;
    private BoxContainer _hologramRoundExtra = default!;
    private BoxContainer _hologramStatus = default!;
    private BoxContainer _hologramNotice = default!;
    private BoxContainer _hologramReady = default!;
    private BoxContainer _hologramSecondary = default!;
    private BoxContainer _hologramActions = default!;
    private BoxContainer _hologramChat = default!;

    private bool _hologramActive;
    private LobbyCrowdSnapshotEvent? _hologramShownSnapshot;
    private bool _hologramRoundShown;
    private string? _hologramShownStart;
    private int _hologramShownPlayers = -1;

    private void InitializeHologram()
    {
        // A flat page in the projection's darkest tone: the cabin fades into exactly this colour.
        _hologramPage = new HologramPage { Visible = false };
        AddChild(_hologramPage);
        SetAnchorPreset(_hologramPage, LayoutPreset.Wide);
        _hologramPage.SetPositionInParent(MainContainer.GetPositionInParent());

        // The classic layout shows the lobby background setting: the projected parallax, or the lobby arts.
        _hologramArt = new HologramLobbyArt { Visible = false };
        AddChild(_hologramArt);
        SetAnchorPreset(_hologramArt, LayoutPreset.Wide);
        _hologramArt.SetPositionInParent(MainContainer.GetPositionInParent());

        BuildHologramLayout();
        MainContainer.AddChild(_hologramLayout);
        InitializeClassicLobby();

        StationTime.AddStyleClass(HologramLobbyStyleClass.Caption);

        // The seated character is the same doll as the stock preview, whenever that one changes.
        CharacterPreview.PreviewChanged += RefreshHologramCharacter;

        UpdateHologram();
    }

    /// <summary>Called every frame: follows the interface style and keeps the round lines fresh.</summary>
    private void UpdateHologram()
    {
        // Two layouts, the shuttle cabin and the classic hologram, move the same controls: whichever goes away is
        // left first, then the other is entered.
        var cabinLobby = _hologramCfg.GetCVar(CCCCVars.HologramLobbyCabin);
        var cabin = DeadSpaceStylePalette.Hologram && cabinLobby;
        var classic = DeadSpaceStylePalette.Hologram && !cabinLobby;

        if (!cabin && _hologramActive)
        {
            _hologramActive = false;
            LeaveHologramLayout();
        }

        if (!classic && _classicActive)
        {
            _classicActive = false;
            LeaveClassicLayout();
        }

        if (cabin && !_hologramActive)
        {
            _hologramActive = true;
            EnterHologramLayout();
        }

        if (classic && !_classicActive)
        {
            _classicActive = true;
            EnterClassicLayout();
        }

        UpdateHologramVisibility();

        if (_hologramActive || _classicActive)
            RefreshHologramRound();

        if (_classicActive)
            UpdateClassicLobby();
    }

    /// <summary>Which arrangement is shown, the character editor and the hide-interface toggle included.</summary>
    private void UpdateHologramVisibility()
    {
        var setup = CharacterSetupState.Visible;
        var art = _hologramCfg.GetCVar(CCCCVars.Background) == nameof(BackgroundType.Image);
        _hologramPage.Visible = _hologramActive;
        _hologramArt.Visible = _classicActive && art;
        _hologramLayout.Visible = _hologramActive && !setup;
        _hologramBody.Visible = !ShowGUI.Pressed;
        UpdateClassicVisibility(art);
        // No server logo on the toolbar in the "Dead Space" lobby.
        LobbyLogo.Visible = !(_hologramActive || _classicActive);

        if (_hologramActive || _classicActive)
            SplitContainer.Visible = false;
        else
            SplitContainer.Visible = !setup;
    }

    private void EnterHologramLayout()
    {
        // The toolbar keeps every link and admin button; vote popups stay under it.
        Move(TopPanel, _hologramTop);
        Move(VoteContainer, _hologramTop);

        Move(StationTime, _hologramRoundExtra);
        Move(LobbySong, _hologramRoundExtra);

        Move(StripeBack, _hologramStatus, c => c.HorizontalAlignment = HAlignment.Stretch);
        // The playtime note concerns joining, so it sits by the ready button.
        Move(PlaytimeComment, _hologramNotice);

        Move(ReadyButton, _hologramReady);
        Move(AshWalkerCancelButton, _hologramReady, Compact);
        Move(ObserveButton, _hologramSecondary, c =>
        {
            Compact(c);
            c.HorizontalExpand = true;
        });
        Move(CallVoteButton, _hologramSecondary, c =>
        {
            Compact(c);
            c.HorizontalExpand = true;
        });
        Move(CharacterSetup, _hologramActions, Compact);
        Move(DonateButton, _hologramActions, Compact);
        Move(OptionsButton, _hologramActions, Compact);
        Move(LeaveButton, _hologramActions, Compact);

        Move(Chat, _hologramChat);
        ApplyHologramChat();

        _hologramShownSnapshot = null;
        _hologramRoundShown = false;
        _hologramShownStart = null;
        _hologramShownPlayers = -1;
        RefreshHologramCharacter();
        _hologramCabin.Refresh();
    }

    private void LeaveHologramLayout()
    {
        ResetHologramChat();
        LobbyLogo.Visible = true;

        // Back in reverse order, so every recorded index is valid again.
        for (var i = _hologramMoved.Count - 1; i >= 0; i--)
        {
            var moved = _hologramMoved[i];
            var control = moved.Control;
            control.Orphan();
            moved.Parent.AddChild(control);
            control.SetPositionInParent(Math.Min(moved.Index, moved.Parent.ChildCount - 1));
            control.HorizontalAlignment = moved.HorizontalAlignment;
            control.HorizontalExpand = moved.HorizontalExpand;
            control.VerticalExpand = moved.VerticalExpand;
            control.MinSize = moved.MinSize;
        }

        _hologramMoved.Clear();
        _hologramCabin.SetSeated(null);
        _hologramCabin.ReleaseDolls();
    }

    /// <summary>
    /// The chat already sits in a projected panel in both layouts: no frame of its own, the messages set off on a
    /// darker inset.
    /// </summary>
    private void ApplyHologramChat()
    {
        Chat.ChatWindowPanel.PanelOverride = new StyleBoxEmpty
        {
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 8,
        };
        var inset = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#02090F").WithAlpha(0.9f),
            BorderColor = DeadSpaceStylePalette.BorderControl,
            BorderThickness = new Thickness(1),
        };
        inset.SetContentMarginOverride(StyleBox.Margin.All, 6);
        Chat.SetOutputBackground(inset);
    }

    private void ResetHologramChat()
    {
        Chat.ChatWindowPanel.PanelOverride = null;
        Chat.SetOutputBackground(null);
    }

    /// <summary>A secondary action: full width of its slot, a lower fixed height.</summary>
    private static void Compact(Control control)
    {
        control.MinSize = new Vector2(0, HologramActionHeight);
    }

    /// <summary>The seated character, and the name with the job they want most.</summary>
    private void RefreshHologramCharacter()
    {
        if (!_hologramActive)
            return;

        _hologramCabin.SetSeated(CharacterPreview.PreviewDummy);

        if (_hologramPreferences.Preferences?.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            _hologramCharacter.Text = _hologramPreferences.ServerDataLoaded
                ? string.Empty
                : Loc.GetString("lobby-character-preview-panel-unloaded-preferences-label");
            return;
        }

        var job = LobbyTopJob.TryGet(profile, _hologramPrototypes, out var top)
            ? top.LocalizedName
            : Loc.GetString("hologram-lobby-no-job");
        _hologramCharacter.Text = Loc.GetString("hologram-lobby-character", ("name", profile.Name), ("job", job));
    }

    /// <summary>Round lines from the server's lobby snapshot, and the arrival line under the title.</summary>
    private void RefreshHologramRound()
    {
        var snapshot = _hologramEntities.System<HologramLobbyCrowdSystem>().Latest;
        var start = StartTime.Text;
        var players = _hologramPlayers.PlayerCount;

        if (_hologramRoundShown
            && ReferenceEquals(snapshot, _hologramShownSnapshot)
            && start == _hologramShownStart
            && (snapshot != null || players == _hologramShownPlayers))
        {
            return;
        }

        _hologramRoundShown = true;
        _hologramShownSnapshot = snapshot;
        _hologramShownStart = start;
        _hologramShownPlayers = players;

        var unknown = Loc.GetString("hologram-lobby-unknown");
        _hologramRoundNumber.Text = snapshot is { RoundId: > 0 } ? $"#{snapshot.RoundId}" : unknown;
        _hologramMode.Text = snapshot?.Mode ?? unknown;
        _hologramMap.Text = snapshot?.Map ?? unknown;
        _hologramPlayersValue.Text = snapshot != null
            ? Loc.GetString("hologram-lobby-players-ready", ("players", snapshot.PlayerCount), ("ready", snapshot.ReadyCount))
            : players.ToString();
        SetClassicRound(_hologramRoundNumber.Text, _hologramMode.Text, _hologramMap.Text, _hologramPlayersValue.Text);

    }

    private void Move(Control control, Control slot, Action<Control>? setup = null)
    {
        if (control.Parent is not { } parent)
            return;

        _hologramMoved.Add(new HologramMovedControl(
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

    private void BuildHologramLayout()
    {
        _hologramTop = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _hologramRoundExtra = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        _hologramStatus = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _hologramNotice = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _hologramReady = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        _hologramSecondary = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
        };
        _hologramActions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        _hologramChat = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _hologramRoundNumber = Value();
        _hologramMode = Value();
        _hologramMap = Value();
        _hologramPlayersValue = Value();
        _hologramCharacter = new Label
        {
            StyleClasses = { HologramLobbyStyleClass.CharacterName },
            HorizontalAlignment = HAlignment.Stretch,
            Align = Label.AlignMode.Center,
            ClipText = true,
        };

        // Left: the round, then readiness.
        var left = new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Surface, HologramLobbyStyleClass.Panel },
            VerticalExpand = true,
            MinWidth = HologramSideWidth,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 10,
                    Children =
                    {
                        Header("hologram-lobby-round-panel"),
                        Row("hologram-lobby-round-number", _hologramRoundNumber),
                        Row("hologram-lobby-round-mode", _hologramMode),
                        Row("hologram-lobby-round-map", _hologramMap),
                        Row("hologram-lobby-round-players", _hologramPlayersValue),
                        _hologramRoundExtra,
                        Header("hologram-lobby-ready-panel"),
                        _hologramStatus,
                        _hologramCharacter,
                        _hologramNotice,
                        _hologramReady,
                        _hologramSecondary,
                        _hologramActions,
                    },
                },
            },
        };

        // Right: the lobby chat.
        var tabs = new TabContainer
        {
            StyleClasses = { HologramMenuStyleClass.Tabs },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _hologramChat },
        };
        TabContainer.SetTabTitle(_hologramChat, Loc.GetString("hologram-lobby-chat-panel").ToUpperInvariant());
        var right = new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Surface, HologramLobbyStyleClass.Panel },
            MinWidth = HologramSideWidth,
            VerticalExpand = true,
            Children = { tabs },
        };

        // Centre: the title and the cabin, the focus of the page.
        _hologramCabin = new HologramShuttleCabin
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var centre = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 2,
            Children =
            {
                new Label
                {
                    Text = Spaced(Loc.GetString("hologram-lobby-title").ToUpperInvariant()),
                    StyleClasses = { HologramLobbyStyleClass.BigTitle },
                    HorizontalAlignment = HAlignment.Center,
                },
                _hologramCabin,
            },
        };

        _hologramBody = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(12, 14, 12, 12),
            Children =
            {
                left,
                centre,
                right,
            },
        };

        _hologramLayout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
            Children = { _hologramTop, _hologramBody },
        };
    }

    private static Label Value()
    {
        return new Label
        {
            StyleClasses = { HologramLobbyStyleClass.Value },
            HorizontalAlignment = HAlignment.Right,
        };
    }

    private static BoxContainer Row(string caption, Label value)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(caption),
                    StyleClasses = { HologramLobbyStyleClass.Caption },
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                },
                value,
            },
        };
    }

    private static PanelContainer Header(string title)
    {
        return new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.SectionHeader, HologramLobbyStyleClass.Header },
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(title).ToUpperInvariant(),
                    StyleClasses = { DeadSpaceStyleClass.Title, HologramLobbyStyleClass.Title },
                    VerticalAlignment = VAlignment.Center,
                },
            },
        };
    }

    /// <summary>Wide tracking for the big title: the engine has no letter spacing, so letters get spaces between them.</summary>
    private static string Spaced(string text)
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

    private readonly record struct HologramMovedControl(
        Control Control,
        Control Parent,
        int Index,
        Control.HAlignment HorizontalAlignment,
        bool HorizontalExpand,
        bool VerticalExpand,
        Vector2 MinSize);

    /// <summary>The page behind the "Dead Space" lobby: one flat colour, the one the cabin fades into.</summary>
    private sealed class HologramPage : Control
    {
        public HologramPage()
        {
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            handle.DrawRect(PixelSizeBox, DeadSpaceStylePalette.SurfaceDark.WithAlpha(1f));
        }
    }
}
