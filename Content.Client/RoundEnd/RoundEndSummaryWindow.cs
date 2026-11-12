using System.Linq;
using System.Numerics;
using Content.Client.DeadSpace.RoundEnd;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Message;
using RoundEndPlayerInfo = Content.Shared.GameTicking.RoundEndMessageEvent.RoundEndPlayerInfo;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.DeadSpace.RoundEnd;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

/// <summary>
/// Window displaying round end information including player manifest.
/// </summary>
public sealed partial class RoundEndSummaryWindow : DefaultWindow
{
    [Dependency] private IEntityManager _entityManager = default!;

    public int RoundId;
    private readonly RoundEndPlayerInfo[] _playersInfo;
    private GridContainer _playerGrid = null!;
    private readonly List<SortButton> _sortButtons = [];
    private string _searchText = string.Empty;

    private enum SortField
    {
        ICName,
        Role,
        PlayerType,
        OOCName
    }

    private SortField _currentSortField = SortField.PlayerType;
    private bool _sortDescending;

    public RoundEndSummaryWindow(string gm, string roundEnd, TimeSpan roundTimeSpan, int roundId, RoundEndPlayerInfo[] info)
    {
        IoCManager.InjectDependencies(this);
        _playersInfo = info;

        MinSize = SetSize = new Vector2(720, 580);

        Title = Loc.GetString("round-end-summary-window-title");

        // The round end window is split into two tabs, one about the round stats
        // and the other is a list of RoundEndPlayerInfo for each player.
        // This tab would be a good place for things like: "x many people died.",
        // "clown slipped the crew x times.", "x shots were fired this round.", etc.
        // Also, good for serious info.

        RoundId = roundId;
        var roundEndTabs = new TabContainer();
        roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId));
        roundEndTabs.AddChild(MakePlayerManifestTab());

        ContentsContainer.AddChild(roundEndTabs);

        OpenCenteredRight();
        MoveToFront();
    }

    private static BoxContainer MakeRoundEndSummaryTab(string gamemode, string roundEnd, TimeSpan roundDuration, int roundId)
    {
<<<<<<< ours
        private readonly IEntityManager _entityManager;
        // DS14-start
        private readonly RoundEndDollPreviewSystem _dollPreviews;
        private readonly List<RoundEndManifestDollView> _manifestDollViews = new();
        private int _dollPreviewOwner;
        private readonly TabContainer _roundEndTabs;
        private Control? _arenaManifestTab;
        // DS14-End
        public int RoundId;
||||||| base
        private readonly IEntityManager _entityManager;
        public int RoundId;
=======
        var roundEndSummaryTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-round-end-summary-tab-title")
        };

        var roundEndSummaryContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10)
        };
        var roundEndSummaryContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical
        };

        //Gamemode Name
        var gamemodeLabel = new RichTextLabel();
        var gamemodeMessage = new FormattedMessage();
        gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-round-id-label", ("roundId", roundId)));
        gamemodeMessage.AddText(" ");
        gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-gamemode-name-label", ("gamemode", gamemode)));
        gamemodeLabel.SetMessage(gamemodeMessage);
        roundEndSummaryContainer.AddChild(gamemodeLabel);

        //Duration
        var roundTimeLabel = new RichTextLabel();
        roundTimeLabel.SetMarkup(Loc.GetString("round-end-summary-window-duration-label",
                                               ("hours", roundDuration.Hours),
                                               ("minutes", roundDuration.Minutes),
                                               ("seconds", roundDuration.Seconds)));
        roundEndSummaryContainer.AddChild(roundTimeLabel);

        //Round end text
        if (!string.IsNullOrEmpty(roundEnd))
        {
            var roundEndLabel = new RichTextLabel();
            roundEndLabel.SetMarkup(roundEnd);
            roundEndSummaryContainer.AddChild(roundEndLabel);
        }

        roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
        roundEndSummaryTab.AddChild(roundEndSummaryContainerScrollbox);

        return roundEndSummaryTab;
    }

    private BoxContainer MakePlayerManifestTab()
    {
        var playerManifestTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-player-manifest-tab-title")
        };

        // Search container
        var searchContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 10, 10, 5)
        };

        var searchLabel = new Label
        {
            Text = "Filter: ",
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(40, 1)
        };
>>>>>>> theirs

        var searchBar = new LineEdit
        {
<<<<<<< ours
            _entityManager = entityManager;
            // DS14-start
            _dollPreviews = entityManager.System<RoundEndDollPreviewSystem>();
            OnClose += ClearManifestDollSnapshots;
            // DS14-end
||||||| base
            _entityManager = entityManager;
=======
            PlaceHolder = Loc.GetString("round-end-summary-window-player-manifest-tab-search-placeholder"),
            HorizontalExpand = true,
            MinSize = new Vector2(200, 1)
        };
>>>>>>> theirs

<<<<<<< ours
            // DS14-start
            var initialWindowSize = GetInitialWindowSize(info);
            MinSize = DefaultWindowSize;
            SetSize = initialWindowSize;
            // DS14-end
||||||| base
            MinSize = SetSize = new Vector2(520, 580);
=======
        searchBar.OnTextChanged += OnSearchTextChanged;
>>>>>>> theirs

        searchContainer.AddChild(searchLabel);
        searchContainer.AddChild(searchBar);
        playerManifestTab.AddChild(searchContainer);

        // Header with sort buttons
        var headerContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 5, 10, 5)
        };

        var icNameButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-character", SortField.ICName);
        var roleButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-role", SortField.Role);
        var playerTypeButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-player-type", SortField.PlayerType);
        var oocNameButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-player", SortField.OOCName);

        playerTypeButton.SetSortIndicator(true);
        headerContainer.AddChild(icNameButton);

        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(roleButton);

        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(playerTypeButton);

<<<<<<< ours
            RoundId = roundId;
            _roundEndTabs = new TabContainer();
            _roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId, info));
            _roundEndTabs.AddChild(MakePlayerManifestTab(info));
||||||| base
            RoundId = roundId;
            var roundEndTabs = new TabContainer();
            roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId));
            roundEndTabs.AddChild(MakePlayerManifestTab(info));
=======
        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(oocNameButton);
>>>>>>> theirs

<<<<<<< ours
            ContentsContainer.AddChild(_roundEndTabs);
||||||| base
            ContentsContainer.AddChild(roundEndTabs);
=======
        playerManifestTab.AddChild(headerContainer);
>>>>>>> theirs

<<<<<<< ours
            OpenCenteredRight();
            ResumeManifestDollSnapshots(); // DS14
            MoveToFront();
||||||| base
            OpenCenteredRight();
            MoveToFront();
=======
        var scrollContainer = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10, 0, 10, 10),
        };

        _playerGrid = new GridContainer
        {
            Columns = 6, // Player Sprite,IC Name,Role,Player Type,OOC Name
            HorizontalExpand = true,
        };

        RefreshPlayerList();

        scrollContainer.AddChild(_playerGrid);
        playerManifestTab.AddChild(scrollContainer);

        return playerManifestTab;
    }

    private SortButton CreateSortButton(string text, SortField field)
    {
        var button = new SortButton(Loc.GetString(text), field);
        button.OnPressed += _ => SortBy(field);
        _sortButtons.Add(button);
        return button;
    }

    /// <summary>
    /// Handles sorting by the specified field, toggling direction if the same field is clicked
    /// </summary>
    private void SortBy(SortField field)
    {
        if (_currentSortField == field)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _currentSortField = field;
            _sortDescending = false;
>>>>>>> theirs
        }

<<<<<<< ours
        private BoxContainer MakeRoundEndSummaryTab(string gamemode, string roundEnd, TimeSpan roundDuration, int roundId,
            RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
||||||| base
        private BoxContainer MakeRoundEndSummaryTab(string gamemode, string roundEnd, TimeSpan roundDuration, int roundId)
=======
        foreach (var button in _sortButtons)
>>>>>>> theirs
        {
            button.SetSortIndicator(button.Field == _currentSortField, _sortDescending);
        }

        RefreshPlayerList();
    }

    /// <summary>
    /// Refreshes the player list grid by clearing it and repopulating with sorted player data
    /// </summary>
    private void RefreshPlayerList()
    {
        _playerGrid.RemoveAllChildren();

        var sortedPlayers = GetSortedPlayers();
        foreach (var playerInfo in sortedPlayers)
        {
            AddPlayerRow(playerInfo);
        }
    }

<<<<<<< ours
            // DS14-start
            var background = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceFlat },
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            // DS14-end

            var roundEndSummaryContainerScrollbox = new ScrollContainer
||||||| base
            var roundEndSummaryContainerScrollbox = new ScrollContainer
=======
    /// <summary>
    /// Adds a single player row to the grid with all columns (sprite, IC name, role, player type, OOC name)
    /// </summary>
    private void AddPlayerRow(RoundEndPlayerInfo playerInfo)
    {
        // Player Sprite column
        if (playerInfo.PlayerNetEntity != null)
        {
            _playerGrid.AddChild(new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
>>>>>>> theirs
            {
<<<<<<< ours
                VerticalExpand = true,
                // DS14-start
                HorizontalExpand = true,
                HScrollEnabled = false,
                // DS14-end
                Margin = new Thickness(10)
            };
            var roundEndSummaryContainer = new BoxContainer
||||||| base
                VerticalExpand = true,
                Margin = new Thickness(10)
            };
            var roundEndSummaryContainer = new BoxContainer
=======
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SetSize = new Vector2(32, 32),
            });
        }
        else
        {
            _playerGrid.AddChild(new Control
>>>>>>> theirs
            {
<<<<<<< ours
                Orientation = LayoutOrientation.Vertical,
                // DS14-start
                SeparationOverride = 10,
                HorizontalExpand = true,
                // DS14-end
            };
||||||| base
                Orientation = LayoutOrientation.Vertical
            };
=======
                SetSize = new Vector2(32, 32),
            });
        }
>>>>>>> theirs

<<<<<<< ours
            // DS14-start
            roundEndSummaryContainer.AddChild(MakeRoundOverviewCard(gamemode, roundDuration, roundId));

            var roundOutcome = GetRoundOutcomeSummary(roundEnd);
            if (roundOutcome != null)
                roundEndSummaryContainer.AddChild(MakeRoundOutcomeCard(roundOutcome));

            roundEndSummaryContainer.AddChild(MakeAntagManifestSection(playersInfo));
            // DS14-end
||||||| base
            //Gamemode Name
            var gamemodeLabel = new RichTextLabel();
            var gamemodeMessage = new FormattedMessage();
            gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-round-id-label", ("roundId", roundId)));
            gamemodeMessage.AddText(" ");
            gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-gamemode-name-label", ("gamemode", gamemode)));
            gamemodeLabel.SetMessage(gamemodeMessage);
            roundEndSummaryContainer.AddChild(gamemodeLabel);

            //Duration
            var roundTimeLabel = new RichTextLabel();
            roundTimeLabel.SetMarkup(Loc.GetString("round-end-summary-window-duration-label",
                                                   ("hours", roundDuration.Hours),
                                                   ("minutes", roundDuration.Minutes),
                                                   ("seconds", roundDuration.Seconds)));
            roundEndSummaryContainer.AddChild(roundTimeLabel);

            //Round end text
            if (!string.IsNullOrEmpty(roundEnd))
            {
                var roundEndLabel = new RichTextLabel();
                roundEndLabel.SetMarkup(roundEnd);
                roundEndSummaryContainer.AddChild(roundEndLabel);
            }
=======
        // IC Name column
        var icNameLabel = new Label
        {
            Text = playerInfo.PlayerICName ?? playerInfo.PlayerOOCName,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        // Apply color coding for antagonists
        if (playerInfo.Antag)
        {
            icNameLabel.FontColorOverride = Color.Red;
        }

        _playerGrid.AddChild(icNameLabel);

        _playerGrid.AddChild(new Control
        {
            SetSize = new Vector2(32, 32),
        });

        // Role column
        var roleLabel = new Label
        {
            Text = playerInfo.Observer ? "-" : Loc.GetString(playerInfo.Role),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };
        _playerGrid.AddChild(roleLabel);

        // Player Type column
        var playerTypeLabel = new Label
        {
            Text = GetPlayerTypeText(playerInfo),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        // Apply color coding based on player type
        if (playerInfo.Antag)
        {
            playerTypeLabel.FontColorOverride = Color.Red;
        }
        else if (playerInfo.Observer)
        {
            playerTypeLabel.FontColorOverride = Color.Gray;
        }

        _playerGrid.AddChild(playerTypeLabel);

        // OOC Name column
        var oocNameLabel = new Label
        {
            Text = playerInfo.PlayerOOCName,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        _playerGrid.AddChild(oocNameLabel);
    }

    /// <summary>
    /// Gets the player type text for a player based on their observer and antagonist flags
    /// </summary>
    private static string GetPlayerTypeText(RoundEndPlayerInfo playerInfo)
    {
        if (playerInfo.Observer)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-observer");
        if (playerInfo.Antag)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-antag");

        return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-crew");
    }

    private IEnumerable<RoundEndPlayerInfo> GetSortedPlayers()
    {
        // First filter players based on search text
        var filteredPlayers = string.IsNullOrEmpty(_searchText)
            ? _playersInfo
            : _playersInfo.Where(PlayerMatchesSearch);

        static string GetIcKey(RoundEndPlayerInfo p) =>
                (p.PlayerICName ?? p.PlayerOOCName).ToLowerInvariant();

        static string GetOocKey(RoundEndPlayerInfo p) =>
            p.PlayerOOCName.ToLowerInvariant();

        static string GetRoleKey(RoundEndPlayerInfo p) =>
            (p.Observer ? "zzz_observer" : p.Role).ToLowerInvariant();

        static int GetPlayerTypeSortKey(RoundEndPlayerInfo p) =>
            p.Antag ? 1 : p.Observer ? 3 : 2;

        return _currentSortField switch
        {
            SortField.ICName => ApplySort(filteredPlayers, GetIcKey, _sortDescending),
            SortField.OOCName => ApplySort(filteredPlayers, GetOocKey, _sortDescending),
            SortField.Role => ApplySort(filteredPlayers, GetRoleKey, _sortDescending),
            SortField.PlayerType => ApplySort(filteredPlayers, GetPlayerTypeSortKey, _sortDescending),
            _ => filteredPlayers
        };
    }

    private static IEnumerable<RoundEndPlayerInfo> ApplySort<TKey>(
        IEnumerable<RoundEndPlayerInfo> players,
        Func<RoundEndPlayerInfo, TKey> primaryKey,
        bool descending)
    {
        static string SecondaryKey(RoundEndPlayerInfo p) =>
            (p.PlayerICName ?? p.PlayerOOCName).ToLowerInvariant();

        return descending
            ? players.OrderByDescending(primaryKey).ThenByDescending(SecondaryKey)
            : players.OrderBy(primaryKey).ThenBy(SecondaryKey);
    }

    /// <summary>
    /// Gets a sort key for player type to ensure consistent ordering: Antagonist -> Crew -> Observer
    /// </summary>

    /// <summary>
    /// Checks if a player matches the current search filter
    /// </summary>
    private bool PlayerMatchesSearch(RoundEndPlayerInfo playerInfo)
    {
        if (string.IsNullOrEmpty(_searchText))
            return true;

        // Search in character name (IC name)
        if (!string.IsNullOrEmpty(playerInfo.PlayerICName) &&
            playerInfo.PlayerICName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;
>>>>>>> theirs

<<<<<<< ours
            roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
            // DS14-start
            background.AddChild(roundEndSummaryContainerScrollbox);
            roundEndSummaryTab.AddChild(background);
            // DS14-end
||||||| base
            roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
            roundEndSummaryTab.AddChild(roundEndSummaryContainerScrollbox);
=======
        // Search in player name (OOC name)
        if (!string.IsNullOrEmpty(playerInfo.PlayerOOCName) &&
            playerInfo.PlayerOOCName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;
>>>>>>> theirs

        // Search in role
        if (!string.IsNullOrEmpty(playerInfo.Role))
        {
            if (playerInfo.Role.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                Loc.GetString(playerInfo.Role).Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Search in player type
        var playerType = GetPlayerTypeText(playerInfo);
        if (playerType.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // Search for "Observer" when they are observers
        if (playerInfo.Observer && "observer".Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Handles search text changes and refreshes the player list
    /// </summary>
    private void OnSearchTextChanged(LineEdit.LineEditEventArgs args)
    {
        _searchText = args.Text;
        RefreshPlayerList();
    }

    private sealed class SortButton : Button
    {
        public SortField Field { get; }
        private readonly Label _sortIndicator;

        public SortButton(string text, SortField field)
        {
            Field = field;
            HorizontalExpand = true;

            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };

<<<<<<< ours
            // DS14-start
            var background = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceFlat },
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            // DS14-end

            var playerInfoContainerScrollbox = new ScrollContainer
||||||| base
            var playerInfoContainerScrollbox = new ScrollContainer
=======
            var label = new Label
>>>>>>> theirs
            {
<<<<<<< ours
                VerticalExpand = true,
                // DS14-start
                HorizontalExpand = true,
                HScrollEnabled = false,
                // DS14-end
                Margin = new Thickness(10)
||||||| base
                VerticalExpand = true,
                Margin = new Thickness(10)
=======
                Text = text,
                HorizontalExpand = true
>>>>>>> theirs
            };

            _sortIndicator = new Label
            {
<<<<<<< ours
                Orientation = LayoutOrientation.Vertical,
                // DS14-start
                SeparationOverride = 8,
                HorizontalExpand = true,
                // DS14-end
||||||| base
                Orientation = LayoutOrientation.Vertical
=======
                Text = "",
                HorizontalAlignment = HAlignment.Right,
                MinSize = new Vector2(15, 1)
>>>>>>> theirs
            };

<<<<<<< ours
            // DS14-start
            var sortedPlayersInfo = playersInfo
                .OrderBy(player => string.IsNullOrWhiteSpace(player.PlayerICName))
                .ThenBy(player => player.PlayerICName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(player => player.PlayerOOCName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            playerInfoContainer.AddChild(MakePlayerManifestHeader(sortedPlayersInfo.Length));
            // DS14-end
||||||| base
            //Put observers at the bottom of the list. Put antags on top.
            var sortedPlayersInfo = playersInfo.OrderBy(p => p.Observer).ThenBy(p => !p.Antag);
=======
            container.AddChild(label);
            container.AddChild(_sortIndicator);

            AddChild(container);
        }
>>>>>>> theirs

<<<<<<< ours
            foreach (var playerInfo in sortedPlayersInfo)
||||||| base
            //Create labels for each player info.
            foreach (var playerInfo in sortedPlayersInfo)
=======
        public void SetSortIndicator(bool active, bool descending = false)
        {
            if (!active)
>>>>>>> theirs
            {
<<<<<<< ours
                playerInfoContainer.AddChild(MakePlayerManifestCard(playerInfo));
            }

            playerInfoContainerScrollbox.AddChild(playerInfoContainer);
            background.AddChild(playerInfoContainerScrollbox);
            playerManifestTab.AddChild(background);
            return playerManifestTab;
        }

        // DS14-start
        private static readonly Color ObjectiveSuccessColor = DeadSpaceStylePalette.PositiveBorderHover;
        private static readonly Color ObjectivePartialSuccessColor = Color.FromHex("#d29922");
        private static readonly Color ObjectivePartialFailureColor = Color.FromHex("#f0883e");
        private static readonly Color ObjectiveFailureColor = DeadSpaceStylePalette.NegativeBorderHover;
        private static readonly Vector2 DefaultWindowSize = new(920, 720);
        private const float RoundEndSummaryWindowHorizontalPadding = 160f;
        private const float AntagManifestCardHorizontalPadding = 20f;
        private const float AntagManifestDollWidth = 160f;
        private const float AntagManifestCardSeparation = 12f;
        private const float PlayerManifestDollWidth = 60f;
        private const float PlayerManifestCardSeparation = 10f;
        private const float ApproximateSubTextGlyphWidth = 8f;
        private const int DetailRowSeparation = 16;

        private Vector2 GetInitialWindowSize(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
        {
            var width = MathF.Max(DefaultWindowSize.X, GetAntagManifestRequiredWindowWidth(playersInfo));
            width = MathF.Max(width, GetPlayerManifestRequiredWindowWidth(playersInfo));
            return DefaultWindowSize with { X = width };
        }

        private float GetAntagManifestRequiredWindowWidth(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
        {
            var maxCardWidth = 0f;

            foreach (var playerInfo in playersInfo.Where(player => player.ShowInAntagManifest))
            {
                var detailRowWidth = MeasureDetailRowWidth(
                    Loc.GetString("round-end-summary-window-antag-manifest-ooc",
                        ("playerOOCName", playerInfo.PlayerOOCName)),
                    GetPlayerManifestRoleText(playerInfo));

                detailRowWidth = MathF.Max(detailRowWidth, MeasureDetailRowWidth(
                    Loc.GetString("round-end-summary-window-antag-manifest-kills", ("kills", playerInfo.ManifestKills)),
                    Loc.GetString("round-end-summary-window-antag-manifest-assists", ("assists", playerInfo.ManifestAssists))));

                var dollWidth = playerInfo.DollData != null
                    ? AntagManifestDollWidth + AntagManifestCardSeparation
                    : 0f;

                maxCardWidth = MathF.Max(maxCardWidth,
                    AntagManifestCardHorizontalPadding + dollWidth + detailRowWidth);
            }

            if (maxCardWidth <= 0f)
                return DefaultWindowSize.X;

            return RoundEndSummaryWindowHorizontalPadding + maxCardWidth;
        }

        private float GetPlayerManifestRequiredWindowWidth(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
        {
            var maxCardWidth = 0f;

            foreach (var playerInfo in playersInfo)
            {
                var detailRowWidth = MeasureDetailRowWidth(
                    Loc.GetString("round-end-summary-window-player-manifest-ooc",
                        ("playerOOCName", playerInfo.PlayerOOCName)),
                    GetPlayerManifestRoleText(playerInfo));

                var dollWidth = playerInfo.DollData != null
                    ? PlayerManifestDollWidth + PlayerManifestCardSeparation
                    : 0f;

                maxCardWidth = MathF.Max(maxCardWidth,
                    AntagManifestCardHorizontalPadding + dollWidth + detailRowWidth);
            }

            if (maxCardWidth <= 0f)
                return DefaultWindowSize.X;

            return RoundEndSummaryWindowHorizontalPadding + maxCardWidth;
        }

        private static float MeasureDetailRowWidth(string left, string right)
        {
            var leftWidth = MeasureSubTextWidth(left);
            var rightWidth = MeasureSubTextWidth(right);

            return leftWidth + rightWidth + DetailRowSeparation;
        }

        private static float MeasureSubTextWidth(string text)
        {
            var label = new Label
            {
                Text = text,
                StyleClasses = { "LabelSubText" },
            };

            label.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            return MathF.Max(label.DesiredSize.X, text.Length * ApproximateSubTextGlyphWidth);
        }

        private Control MakeRoundOverviewCard(string gamemode, TimeSpan roundDuration, int roundId)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10),
                SeparationOverride = 4,
                HorizontalExpand = true,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-round-card-title", ("roundId", roundId)),
                StyleClasses = { "LabelBig" },
                HorizontalExpand = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-round-card-gamemode", ("gamemode", gamemode)),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-round-card-duration",
                    ("hours", roundDuration.Hours),
                    ("minutes", roundDuration.Minutes),
                    ("seconds", roundDuration.Seconds)),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });

            panel.AddChild(box);
            return panel;
        }

        private static Control MakeRoundOutcomeCard(string roundOutcome)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10),
                SeparationOverride = 4,
                HorizontalExpand = true,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-result-card-title"),
                StyleClasses = { "LabelHeading" },
                HorizontalExpand = true,
            });

            var outcomeLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                MinHeight = 34,
            };
            outcomeLabel.SetMarkup($"[font size=18]{roundOutcome}[/font]");
            box.AddChild(outcomeLabel);

            panel.AddChild(box);
            return panel;
        }

        private Control MakePlayerManifestHeader(int playerCount)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10),
                SeparationOverride = 4,
                HorizontalExpand = true,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-player-manifest-tab-title"),
                StyleClasses = { "LabelBig" },
                HorizontalExpand = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-player-manifest-subtitle", ("count", playerCount)),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });

            panel.AddChild(box);
            return panel;
        }

        private Control MakePlayerManifestCard(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var card = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(10),
                SeparationOverride = 10,
                HorizontalExpand = true,
            };

            var doll = MakePlayerManifestDoll(playerInfo);
            if (doll != null)
                card.AddChild(doll);

            var content = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 5,
                HorizontalExpand = true,
                VerticalExpand = true,
            };

            content.AddChild(MakeManifestName(playerInfo, playerInfo.Antag)); // DS-14

            var roleText = GetPlayerManifestRoleText(playerInfo);

            content.AddChild(MakeDetailRow(
                Loc.GetString("round-end-summary-window-player-manifest-ooc",
                    ("playerOOCName", playerInfo.PlayerOOCName)),
                roleText));
            content.AddChild(MakeQuoteLabel(playerInfo.ManifestQuote));

            card.AddChild(content);
            panel.AddChild(card);
            return panel;
        }

        private Control? MakePlayerManifestDoll(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            return MakeManifestDoll(playerInfo.DollData, new Vector2(60, 60), new Vector2(58, 58), new Vector2(1.45f, 1.45f));
        }

        private Control MakeAntagManifestSection(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
        {
            var antags = playersInfo
                .Where(player => player.ShowInAntagManifest)
                .OrderByDescending(player => player.ManifestKills)
                .ThenByDescending(player => player.ManifestAssists)
                .ThenBy(GetRoleSortKey)
                .ThenBy(player => player.PlayerOOCName)
                .ToArray();

            var root = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(0, 10, 0, 0),
                SeparationOverride = 10,
                HorizontalExpand = true,
            };

            root.AddChild(MakeManifestHeader(antags.Length));

            if (antags.Length == 0)
            {
                root.AddChild(MakeEmptyManifestPanel());
                return root;
            }

            var firstAntagCard = true;
            foreach (var playerInfo in antags)
            {
                if (!firstAntagCard)
                    root.AddChild(MakeManifestCardSeparator());

                root.AddChild(MakeAntagManifestCard(playerInfo));
                firstAntagCard = false;
            }

            return root;
        }

        private static Control MakeManifestCardSeparator()
        {
            return new PanelContainer
            {
                MinHeight = 1,
                HorizontalExpand = true,
                Margin = new Thickness(8, 0),
                StyleClasses = { DeadSpaceStyleClass.AccentDim },
            };
        }

        private Control MakeManifestHeader(int antagCount)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(10),
                SeparationOverride = 10,
                HorizontalExpand = true,
            };

            var labels = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            labels.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-antag-manifest-title"),
                StyleClasses = { "LabelBig" },
                HorizontalExpand = true,
            });
            labels.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-antag-manifest-subtitle", ("count", antagCount)),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });

            box.AddChild(labels);
            panel.AddChild(box);
            return panel;
        }

        // DS-14-Start
        private static Control MakeManifestName(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo, bool antag)
        {
            var name = playerInfo.PlayerICName ?? Loc.GetString("generic-unknown-title");
            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };

            if (!string.IsNullOrWhiteSpace(playerInfo.PlayerPseudonym))
            {
                container.AddChild(new Label
                {
                    Text = playerInfo.PlayerPseudonym,
                    StyleClasses = { antag ? "LabelBig" : "LabelHeading" },
                    ClipText = true,
                    HorizontalExpand = true,
                });
                container.AddChild(new Label
                {
                    Text = name,
                    StyleClasses = { "LabelSubText" },
                    ClipText = true,
                    HorizontalExpand = true,
                });
            }
            else
            {
                container.AddChild(new Label
                {
                    Text = name,
                    StyleClasses = { antag ? "LabelBig" : "LabelHeading" },
                    ClipText = true,
                    HorizontalExpand = true,
                });
            }

            return container;
        }
        // DS-14-End

        private Control MakeAntagManifestCard(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var card = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(10),
                SeparationOverride = 12,
                HorizontalExpand = true,
            };

            var doll = MakeAntagDoll(playerInfo);
            if (doll != null)
                card.AddChild(doll);

            var content = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 6,
                HorizontalExpand = true,
                VerticalExpand = true,
            };

            var nameRow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };
            nameRow.AddChild(MakeManifestName(playerInfo, true)); // DS-14
            if (playerInfo.IsDead)
            {
                var badgeLabel = new RichTextLabel
                {
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                badgeLabel.SetMarkup(Loc.GetString("objectives-eliminated"));
                nameRow.AddChild(badgeLabel);
            }
            else if (playerInfo.InCustody)
            {
                var badgeLabel = new RichTextLabel
                {
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                badgeLabel.SetMarkup(Loc.GetString("objectives-in-custody"));
                nameRow.AddChild(badgeLabel);
            }
            content.AddChild(nameRow);

            content.AddChild(MakeDetailRow(
                Loc.GetString("round-end-summary-window-antag-manifest-ooc",
                    ("playerOOCName", playerInfo.PlayerOOCName)),
                GetPlayerManifestRoleText(playerInfo)));

            content.AddChild(MakeDetailRow(
                Loc.GetString("round-end-summary-window-antag-manifest-kills", ("kills", playerInfo.ManifestKills)),
                Loc.GetString("round-end-summary-window-antag-manifest-assists", ("assists", playerInfo.ManifestAssists))));

            content.AddChild(MakeQuoteLabel(playerInfo.ManifestQuote));

            var objectives = playerInfo.ManifestObjectives ?? Array.Empty<RoundEndMessageEvent.RoundEndObjectiveInfo>();
            if (objectives.Length > 0)
                content.AddChild(MakeObjectivesTable(objectives));

            card.AddChild(content);
            panel.AddChild(card);
            return panel;
        }

        private static Control MakeDetailRow(string left, string right)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = DetailRowSeparation,
                HorizontalExpand = true,
            };

            row.AddChild(MakeDetailLabel(left));
            row.AddChild(MakeDetailLabel(right));
            return row;
        }

        private static Label MakeDetailLabel(string text)
        {
            return new Label
            {
                Text = text,
                StyleClasses = { "LabelSubText" },
                ClipText = true,
                MinWidth = MeasureSubTextWidth(text),
                HorizontalExpand = true,
            };
        }

        private static Control MakeQuoteLabel(string quote)
        {
            var displayQuote = string.IsNullOrWhiteSpace(quote)
                ? Loc.GetString("round-end-summary-window-antag-manifest-quote-fallback")
                : quote;

            var quoteLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                MinHeight = 46,
            };
            quoteLabel.SetMarkup(Loc.GetString("round-end-summary-window-antag-manifest-quote",
                ("quote", FormattedMessage.EscapeText(displayQuote))));
            return quoteLabel;
        }

        private static Control MakeObjectivesTable(RoundEndMessageEvent.RoundEndObjectiveInfo[] objectives)
        {
            var root = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 4,
                HorizontalExpand = true,
                Margin = new Thickness(0, 4, 0, 0),
            };

            root.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-objectives-table-title"),
                StyleClasses = { "LabelHeading" },
                HorizontalExpand = true,
            });

            var table = new TableContainer
            {
                Columns = 2,
                MinForcedColumnWidth = 170,
                HorizontalExpand = true,
            };

            AddObjectivesTableRow(
                table,
                Loc.GetString("round-end-summary-window-objectives-table-objective"),
                Loc.GetString("round-end-summary-window-objectives-table-status"),
                header: true);

            foreach (var objective in objectives)
            {
                var (status, color) = GetObjectiveStatus(objective.Progress);
                AddObjectivesTableRow(table, objective.Title, status, color);
            }

            root.AddChild(table);
            return root;
        }

        private static void AddObjectivesTableRow(
            TableContainer table,
            string objective,
            string status,
            Color? statusColor = null,
            bool header = false)
        {
            var objectiveLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Top,
            };
            objectiveLabel.SetMarkup(header
                ? $"[bold]{FormattedMessage.EscapeText(objective)}[/bold]"
                : FormattedMessage.EscapeText(objective));

            var statusLabel = new Label
            {
                Text = status,
                StyleClasses = { header ? "LabelSubText" : "Label" },
                ClipText = true,
                MinWidth = 150,
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Top,
            };

            if (statusColor != null)
                statusLabel.FontColorOverride = statusColor.Value;

            table.AddChild(objectiveLabel);
            table.AddChild(statusLabel);
        }

        private static (string Status, Color Color) GetObjectiveStatus(float progress)
        {
            var clampedProgress = Math.Clamp(progress, 0f, 1f);
            var progressPercent = Math.Round(clampedProgress * 100f);

            if (clampedProgress > 0.99f)
            {
                return (Loc.GetString("round-end-summary-window-objectives-status-success",
                    ("progress", progressPercent)), ObjectiveSuccessColor);
            }

            if (clampedProgress >= 0.5f)
            {
                return (Loc.GetString("round-end-summary-window-objectives-status-partial-success",
                    ("progress", progressPercent)), ObjectivePartialSuccessColor);
            }

            if (clampedProgress > 0f)
            {
                return (Loc.GetString("round-end-summary-window-objectives-status-partial-failure",
                    ("progress", progressPercent)), ObjectivePartialFailureColor);
            }

            return (Loc.GetString("round-end-summary-window-objectives-status-failure",
                ("progress", progressPercent)), ObjectiveFailureColor);
        }

        private Control? MakeAntagDoll(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            return MakeManifestDoll(playerInfo.DollData, new Vector2(160, 160), new Vector2(160, 160), new Vector2(3f, 3f));
        }

        private void ClearManifestDollSnapshots()
        {
            if (_dollPreviewOwner != 0)
                _dollPreviews.Cancel(_dollPreviewOwner);

            _dollPreviewOwner = 0;

            foreach (var view in _manifestDollViews)
                view.ClearSnapshot();
        }

        public void ResumeManifestDollSnapshots()
        {
            if (_dollPreviewOwner != 0)
                return;

            _dollPreviewOwner = _dollPreviews.CreateOwner();
            foreach (var view in _manifestDollViews)
                view.RequestSnapshot(_dollPreviewOwner);
        }

        // DS14-start
        public void SetArenaManifest(ArenaManifestEvent ev)
        {
            if (_arenaManifestTab != null)
            {
                _roundEndTabs.RemoveChild(_arenaManifestTab);
                _arenaManifestTab.Dispose();
                _arenaManifestTab = null;
            }

            _arenaManifestTab = MakeArenaManifestTab(ev);
            if (_arenaManifestTab != null)
                _roundEndTabs.AddChild(_arenaManifestTab);
        }

        private static readonly Color ArenaFirstPlaceColor = Color.FromHex("#ffd700");
        private static readonly Color ArenaSecondPlaceColor = Color.FromHex("#c0c0c0");
        private static readonly Color ArenaThirdPlaceColor = Color.FromHex("#cd7f32");

        private Control? MakeArenaManifestTab(ArenaManifestEvent ev)
        {
            if (ev.Players.Count == 0)
                return null;

            var tab = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Name = Loc.GetString("arena-manifest-tab-title"),
            };

            var background = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceFlat },
                HorizontalExpand = true,
                VerticalExpand = true,
            };

            var scrollbox = new ScrollContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                HScrollEnabled = false,
                Margin = new Thickness(10),
            };

            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 8,
                HorizontalExpand = true,
            };

            var topThree = ev.Players.Take(3).ToArray();
            if (topThree.Length > 0)
            {
                var podium = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 16,
                    HorizontalAlignment = HAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12),
                };

                for (var i = 0; i < topThree.Length; i++)
                    podium.AddChild(MakeArenaPodiumCard(i + 1, topThree[i]));

                container.AddChild(podium);
            }

            container.AddChild(MakeArenaManifestHeader(ev.Players.Count));

            for (var i = 0; i < ev.Players.Count; i++)
                container.AddChild(MakeArenaManifestRow(i + 1, ev.Players[i]));

            scrollbox.AddChild(container);
            background.AddChild(scrollbox);
            tab.AddChild(background);
            return tab;
        }

        private static Control MakeArenaPodiumCard(int place, ArenaPlayerRecord player)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                MinWidth = 200,
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 4,
                Margin = new Thickness(12),
                HorizontalAlignment = HAlignment.Center,
                HorizontalExpand = true,
            };

            var placeColor = place switch
            {
                1 => ArenaFirstPlaceColor,
                2 => ArenaSecondPlaceColor,
                _ => ArenaThirdPlaceColor,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-place", ("place", place)),
                StyleClasses = { "LabelBig" },
                FontColorOverride = placeColor,
                HorizontalAlignment = HAlignment.Center,
            });
            box.AddChild(new Label
            {
                Text = string.IsNullOrEmpty(player.PlayerName)
                    ? Loc.GetString("arena-manifest-unknown-player")
                    : player.PlayerName,
                StyleClasses = { "LabelHeading" },
                HorizontalAlignment = HAlignment.Center,
                HorizontalExpand = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-kd",
                    ("kills", player.Kills),
                    ("deaths", player.Deaths),
                    ("kd", player.KD.ToString("0.##"))),
                StyleClasses = { "LabelSubText" },
                HorizontalAlignment = HAlignment.Center,
            });

            panel.AddChild(box);
            return panel;
        }

        private static Control MakeArenaManifestHeader(int playerCount)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(10),
                SeparationOverride = 4,
                HorizontalExpand = true,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-tab-title"),
                StyleClasses = { "LabelBig" },
                HorizontalExpand = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-subtitle", ("count", playerCount)),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });

            panel.AddChild(box);
            return panel;
        }

        private static Control MakeArenaManifestRow(int place, ArenaPlayerRecord player)
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(10),
                SeparationOverride = 10,
                HorizontalExpand = true,
            };

            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-row-place", ("place", place)),
                MinWidth = 60,
                StyleClasses = { "LabelSubText" },
            });
            box.AddChild(new Label
            {
                Text = string.IsNullOrEmpty(player.PlayerName)
                    ? Loc.GetString("arena-manifest-unknown-player")
                    : player.PlayerName,
                StyleClasses = { "LabelHeading" },
                HorizontalExpand = true,
                ClipText = true,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-row-kills", ("kills", player.Kills)),
                MinWidth = 70,
                HorizontalAlignment = HAlignment.Right,
            });
            box.AddChild(new Label
            {
                Text = Loc.GetString("arena-manifest-row-deaths", ("deaths", player.Deaths)),
                MinWidth = 70,
                HorizontalAlignment = HAlignment.Right,
            });
            box.AddChild(new Label
            {
                Text = player.KD.ToString("0.##"),
                MinWidth = 70,
                HorizontalAlignment = HAlignment.Right,
            });

            panel.AddChild(box);
            return panel;
        }
        // DS14-end

        private Control? MakeManifestDoll(
            RoundEndDollData? dollData,
            Vector2 panelSize,
            Vector2 viewSize,
            Vector2 scale)
        {
            if (dollData == null)
                return null;

            var panel = new PanelContainer
            {
                SetSize = panelSize,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = DeadSpaceStylePalette.SurfaceInset,
                    BorderColor = DeadSpaceStylePalette.BorderInset,
                    BorderThickness = new Thickness(1),
                },
            };

            var view = new RoundEndManifestDollView(dollData, _dollPreviews, _entityManager)
            {
                OverrideDirection = Direction.South,
                SetSize = viewSize,
                Scale = scale,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };
            view.SnapshotFailed += () => panel.Visible = false;
            _manifestDollViews.Add(view);
            panel.AddChild(view);

            return panel;
        }

        private static Control MakeEmptyManifestPanel()
        {
            var panel = new PanelContainer
            {
                StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
                HorizontalExpand = true,
            };

            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(10),
                SeparationOverride = 10,
                HorizontalExpand = true,
            };

            var labels = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            labels.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-antag-manifest-empty-title"),
                StyleClasses = { "LabelHeading" },
                HorizontalExpand = true,
            });
            labels.AddChild(new Label
            {
                Text = Loc.GetString("round-end-summary-window-antag-manifest-empty-subtitle"),
                StyleClasses = { "LabelSubText" },
                HorizontalExpand = true,
            });

            box.AddChild(labels);
            panel.AddChild(box);
            return panel;
        }

        private static string GetAntagRolesText(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            var roles = playerInfo.AntagRoleNames ?? Array.Empty<string>();
            if (roles.Length == 0 && !string.IsNullOrWhiteSpace(playerInfo.Role))
                roles = new[] { playerInfo.Role };

            if (roles.Length == 0)
                return Loc.GetString("game-ticker-unknown-role");

            return string.Join(", ", roles.Select(LocalizeOrRaw));
        }

        private static string GetJobRolesText(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            var roles = playerInfo.JobRoleNames ?? Array.Empty<string>();
            if (roles.Length == 0 && !string.IsNullOrWhiteSpace(playerInfo.Role))
                roles = new[] { playerInfo.Role };

            if (roles.Length == 0)
                return Loc.GetString("game-ticker-unknown-role");

            return string.Join(", ", roles.Select(LocalizeOrRaw));
        }

        private static string GetPlayerManifestRoleText(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            if (playerInfo.Observer)
                return Loc.GetString("round-end-summary-window-player-manifest-observer");

            var hasStationRole = (playerInfo.JobRoleNames ?? Array.Empty<string>()).Length > 0;
            var jobRoles = GetJobRolesText(playerInfo);
            var antagRoles = playerInfo.AntagRoleNames ?? Array.Empty<string>();
            if (hasStationRole && antagRoles.Length > 0)
            {
                return Loc.GetString("round-end-summary-window-player-manifest-role-with-antagonist",
                    ("role", jobRoles),
                    ("antagonist", GetAntagRolesText(playerInfo)));
            }

            return Loc.GetString("round-end-summary-window-player-manifest-role", ("role", jobRoles));
        }

        private static string? GetRoundOutcomeSummary(string roundEnd)
        {
            if (string.IsNullOrWhiteSpace(roundEnd))
                return null;

            var lines = new List<string>();

            foreach (var line in roundEnd
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                lines.Add(trimmed);
            }

            return lines.Count == 0 ? null : string.Join('\n', lines);
        }

        private static string GetRoleSortKey(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
        {
            var roles = playerInfo.AntagRoleNames ?? Array.Empty<string>();
            if (roles.Length > 0)
                return roles[0];

            return playerInfo.Role ?? string.Empty;
        }

        private static string LocalizeOrRaw(string value)
        {
            return Loc.TryGetString(value, out var localized) ? localized : value;
        }

        private sealed class RoundEndManifestDollView : SpriteView
        {
            private readonly RoundEndDollData _data;
            private readonly RoundEndDollPreviewSystem _previews;
            private int _owner;
            private bool _requestActive;

            public event Action? SnapshotFailed;

            public RoundEndManifestDollView(
                RoundEndDollData data,
                RoundEndDollPreviewSystem previews,
                IEntityManager entityManager) : base(entityManager)
            {
                _data = data;
                _previews = previews;
            }

            public void RequestSnapshot(int owner)
            {
                _owner = owner;
                _requestActive = true;
                SetEntity(null);
                _previews.Request(owner, _data, snapshot => OnSnapshotReady(owner, snapshot));
            }

            private void OnSnapshotReady(int owner, EntityUid? snapshot)
            {
                if (!_requestActive || owner != _owner)
                    return;

                if (snapshot == null)
                {
                    SnapshotFailed?.Invoke();
                    return;
                }
||||||| base
                var hBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                };

                var playerInfoText = new RichTextLabel
                {
                    VerticalAlignment = VAlignment.Center,
                    VerticalExpand = true,
                };

                if (playerInfo.PlayerNetEntity != null)
                {
                    hBox.AddChild(new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
                        {
                            OverrideDirection = Direction.South,
                            VerticalAlignment = VAlignment.Center,
                            SetSize = new Vector2(32, 32),
                            VerticalExpand = true,
                        });
                }

                if (playerInfo.PlayerICName != null)
                {
                    if (playerInfo.Observer)
                    {
                        playerInfoText.SetMarkup(
                            Loc.GetString("round-end-summary-window-player-info-if-observer-text",
                                          ("playerOOCName", playerInfo.PlayerOOCName),
                                          ("playerICName", playerInfo.PlayerICName)));
                    }
                    else
                    {
                        //TODO: On Hover display a popup detailing more play info.
                        //For example: their antag goals and if they completed them sucessfully.
                        var icNameColor = playerInfo.Antag ? "red" : "white";
                        playerInfoText.SetMarkup(
                            Loc.GetString("round-end-summary-window-player-info-if-not-observer-text",
                                ("playerOOCName", playerInfo.PlayerOOCName),
                                ("icNameColor", icNameColor),
                                ("playerICName", playerInfo.PlayerICName),
                                ("playerRole", Loc.GetString(playerInfo.Role))));
                    }
                }
                hBox.AddChild(playerInfoText);
                playerInfoContainer.AddChild(hBox);
            }
=======
                _sortIndicator.Text = "";
                return;
            }
>>>>>>> theirs

<<<<<<< ours
                SetEntity(snapshot);
            }

            public void ClearSnapshot()
            {
                _requestActive = false;
                SetEntity(null);
            }
||||||| base
            playerInfoContainerScrollbox.AddChild(playerInfoContainer);
            playerManifestTab.AddChild(playerInfoContainerScrollbox);

            return playerManifestTab;
=======
            _sortIndicator.Text = descending ? "▼" : "▲";
>>>>>>> theirs
        }
        // DS14-end
    }
}
