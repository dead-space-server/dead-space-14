// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.PdaArcade;
using Robust.Shared.Network;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaArcadeFragment : BoxContainer
{
    public event Action<string, int>? ScoreReady;
    public event Action? NearbyReady;
    public event Action<string, NetEntity>? InviteReady;
    public event Action<string>? MoveReady;
    public event Action? LeaveReady;
    public event Action? AgainReady;

    private readonly BoxContainer _menu;
    private readonly BoxContainer _solo;
    private readonly BoxContainer _play;
    private readonly BoxContainer _scores;
    private readonly BoxContainer _coop;
    private readonly BoxContainer _people;
    private readonly BoxContainer _match;
    private readonly PdaTableBoard _table;
    private readonly Button _again;
    private readonly BoxContainer _host;
    private PanelContainer _overlay = default!;
    private Label _overlayTitle = default!;
    private BoxContainer _overlayRows = default!;
    private readonly Label _title;
    private readonly Label _score;
    private readonly BoxContainer _scoreList;
    private Button _resumeButton = default!;
    private Button _finishButton = default!;
    private PdaArcadeBoard? _board;
    private string _game = "";
    private PdaArcadeEntry[] _entries = Array.Empty<PdaArcadeEntry>();
    private PdaArcadePerson[] _nearby = Array.Empty<PdaArcadePerson>();
    private string _together = "";

    public PdaArcadeFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Stop;

        _menu = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
        _solo = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
        _play = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true, Visible = false };
        _scores = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true, Visible = false };
        _coop = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true, Visible = false };
        _people = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        _match = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true, Visible = false };
        _table = new PdaTableBoard();
        _again = new Button { Text = Loc.GetString("pda-arcade-again"), Visible = false, Margin = new Thickness(4, 2) };
        _again.OnPressed += _ => AgainReady?.Invoke();
        _table.Moved += move => MoveReady?.Invoke(move);
        _table.FocusReady += GrabKeyboardFocus;

        AddChild(_menu);
        AddChild(_play);
        AddChild(_scores);
        AddChild(_match);

        var tabs = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(6, 4) };
        var soloTab = new Button { Text = Loc.GetString("pda-arcade-solo"), Margin = new Thickness(2) };
        var togetherTab = new Button { Text = Loc.GetString("pda-arcade-together"), Margin = new Thickness(2) };
        soloTab.OnPressed += _ => ShowTab(false);
        togetherTab.OnPressed += _ => ShowTab(true);
        tabs.AddChild(soloTab);
        tabs.AddChild(togetherTab);
        _menu.AddChild(tabs);
        _menu.AddChild(_solo);
        _menu.AddChild(_coop);
        foreach (var game in PdaArcadeArt.Games())
        {
            if (!game.Together)
                AddGameButton(game);
        }
        var boardButton = new Button { Text = Loc.GetString("pda-arcade-board"), Margin = new Thickness(6, 8, 6, 4) };
        boardButton.OnPressed += _ => ShowScores();
        _menu.AddChild(boardButton);
        BuildTogether();
        var matchTop = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(4, 2) };
        var matchBack = new Button { Text = Loc.GetString("pda-arcade-back") };
        matchBack.OnPressed += _ => LeaveReady?.Invoke();
        matchTop.AddChild(matchBack);
        matchTop.AddChild(_again);
        _match.AddChild(matchTop);
        _match.AddChild(_table);

        var top = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(4, 2) };
        var back = new Button { Text = Loc.GetString("pda-arcade-back") };
        back.OnPressed += _ => LeaveGame();
        _title = new Label { Margin = new Thickness(8, 0, 0, 0), HorizontalExpand = true };
        _score = new Label { HorizontalAlignment = HAlignment.Right };
        top.AddChild(back);
        top.AddChild(_title);
        top.AddChild(_score);

        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, VerticalExpand = true, HorizontalExpand = true };
        var stage = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true, HorizontalExpand = true };
        _host = new BoxContainer { VerticalExpand = true, HorizontalExpand = true, RectClipContent = true };
        _overlay = BuildOverlay();
        stage.AddChild(_host);
        stage.AddChild(_overlay);
        row.AddChild(stage);
        row.AddChild(BuildPyramid());
        _play.AddChild(top);
        _play.AddChild(row);

        CanKeyboardFocus = true;
        OnKeyBindDown += args => RouteKey(args, true);
        OnKeyBindUp += args => RouteKey(args, false);

        var scoreTop = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(4, 2) };
        var scoreBack = new Button { Text = Loc.GetString("pda-arcade-back") };
        scoreBack.OnPressed += _ => ShowMenu();
        scoreTop.AddChild(scoreBack);
        scoreTop.AddChild(new Label { Text = Loc.GetString("pda-arcade-board"), Margin = new Thickness(8, 0, 0, 0) });
        _scores.AddChild(scoreTop);
        _scoreList = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        _scores.AddChild(new ScrollContainer { VerticalExpand = true, Children = { _scoreList } });
    }

    private bool _steering;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var steering = VisibleInTree && ContainsFocus(UserInterfaceManager.KeyboardFocused);
        if (_steering && !steering)
            ReleaseSteer();

        _steering = steering;
    }

    private void ReleaseSteer()
    {
        if (_board == null)
            return;

        _board.Command("left", false);
        _board.Command("right", false);
        _board.Command("up", false);
        _board.Command("down", false);
    }

    private bool ContainsFocus(Control? control)
    {
        while (control != null)
        {
            if (control == this)
                return true;

            control = control.Parent;
        }

        return false;
    }

    public void Apply(PdaArcadeUiState state)
    {
        _entries = state.Entries;
        _nearby = state.Nearby;
        if (_scores.Visible)
            FillScores();
        if (_coop.Visible)
            FillPeople();

        if (state.InMatch)
            ShowMatch(state);
        else if (_match.Visible)
            ShowMenu();
    }

    private void ShowTab(bool together)
    {
        _solo.Visible = !together;
        _coop.Visible = together;
        if (together)
            NearbyReady?.Invoke();
    }

    private void ShowMatch(PdaArcadeUiState state)
    {
        _menu.Visible = false;
        _play.Visible = false;
        _scores.Visible = false;
        _match.Visible = true;
        _table.Game = state.MatchGame;
        _table.Board = state.MatchBoard;
        _table.Yours = state.MatchYours && !state.MatchOver;
        _table.Rolling = state.Rolling;
        _table.YourDie = state.YourDie;
        _table.TheirDie = state.TheirDie;
        _table.MineBottom = state.MineBottom;
        _again.Visible = state.MatchOver;
        GrabKeyboardFocus();
    }

    private void BuildTogether()
    {
        var back = new Button { Text = Loc.GetString("pda-arcade-back"), Margin = new Thickness(6, 4) };
        back.OnPressed += _ => ShowMenu();
        _coop.AddChild(back);
        _coop.AddChild(new Label { Text = Loc.GetString("pda-arcade-together"), Margin = new Thickness(6, 2) });
        foreach (var game in PdaArcadeGame.Together)
        {
            var id = game;
            var button = new Button { Text = Loc.GetString("pda-arcade-game-" + id), Margin = new Thickness(6, 2) };
            button.OnPressed += _ =>
            {
                _together = id;
                NearbyReady?.Invoke();
                FillPeople();
            };
            _coop.AddChild(button);
        }

        _coop.AddChild(_people);
    }

    private void FillPeople()
    {
        _people.RemoveAllChildren();
        if (_together == "")
            return;

        _people.AddChild(new Label
        {
            Text = Loc.GetString("pda-arcade-pick", ("game", Loc.GetString("pda-arcade-game-" + _together))),
            Margin = new Thickness(6, 6, 6, 2),
        });
        if (_nearby.Length == 0)
        {
            _people.AddChild(new Label { Text = Loc.GetString("pda-arcade-nobody"), Margin = new Thickness(6) });
            return;
        }

        foreach (var person in _nearby)
        {
            var target = person;
            var button = new Button { Text = person.Name, Margin = new Thickness(6, 2) };
            button.OnPressed += _ => InviteReady?.Invoke(_together, target.Entity);
            _people.AddChild(button);
        }
    }

    private static readonly Dictionary<string, Func<PdaArcadeBoard>> Boards = new()
    {
        [PdaArcadeGame.Snake] = () => new PdaSnakeBoard(),
        [PdaArcadeGame.Tiles] = () => new PdaTilesBoard(),
        [PdaArcadeGame.Blocks] = () => new PdaBlocksBoard(),
        [PdaArcadeGame.Rocks] = () => new PdaRocksBoard(),
        [PdaArcadeGame.Flap] = () => new PdaFlapBoard(),
        [PdaArcadeGame.Match] = () => new PdaMatchBoard(),
        [PdaArcadeGame.Deck] = () => new PdaDeckBoard(),
        [PdaArcadeGame.Road] = () => new PdaRoadBoard(),
    };

    private void AddGameButton(PdaArcadeGamePrototype game)
    {
        var button = new Button
        {
            Text = Loc.GetString(game.Name),
            Margin = new Thickness(6, 2),
        };
        button.OnPressed += _ => Open(game);
        _solo.AddChild(button);
    }

    private void Open(PdaArcadeGamePrototype game)
    {
        _game = game.ID;
        _board?.Orphan();
        if (!Boards.TryGetValue(game.ID, out var make))
            return;

        _board = make();
        _board.Bind(game.ID);

        _board.Finished += score =>
        {
            _score.Text = Loc.GetString("pda-arcade-over", ("score", score));
            ScoreReady?.Invoke(_game, score);
            ShowGameBoard((_board.Won ? Loc.GetString("pda-arcade-win") : Loc.GetString("pda-arcade-lose")) + " — " + score);
        };
        _board.Scored += value => _score.Text = Loc.GetString("pda-arcade-score", ("score", value));
        _board.Restart();
        _host.AddChild(_board);
        _title.Text = Loc.GetString(game.Name);
        _score.Text = Loc.GetString("pda-arcade-score", ("score", 0));
        _resumeButton.Text = game.Quiet ? Loc.GetString("pda-arcade-board") : Loc.GetString("pda-arcade-pause");
        _menu.Visible = false;
        _scores.Visible = false;
        _play.Visible = true;
        _board.GrabKeyboardFocus();
        GrabKeyboardFocus();
        HideOverlay();
    }

    private BoxContainer BuildPyramid()
    {
        var side = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 96,
            Margin = new Thickness(2),
        };
        side.AddChild(Arrow("▲", () => Tap("up")));
        var mid = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalAlignment = HAlignment.Center };
        mid.AddChild(Arrow("◄", () => Tap("left")));
        mid.AddChild(Arrow("►", () => Tap("right")));
        side.AddChild(mid);
        side.AddChild(Arrow("▼", () => Tap("down")));
        side.AddChild(Arrow("␣", () => Tap("act")));
        _resumeButton = new Button { Text = Loc.GetString("pda-arcade-pause"), Margin = new Thickness(4), HorizontalAlignment = HAlignment.Center };
        _resumeButton.OnPressed += _ => ToggleSide();
        side.AddChild(_resumeButton);
        return side;
    }

    private PanelContainer BuildOverlay()
    {
        var panel = new PanelContainer
        {
            Visible = false,
            VerticalExpand = true,
            Margin = new Thickness(8),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1b1e2acc"),
                BorderColor = Color.FromHex("#f2d36b"),
                BorderThickness = new Thickness(2),
            },
        };
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(10) };
        _overlayTitle = new Label { Align = Label.AlignMode.Center, Margin = new Thickness(0, 4) };
        _overlayRows = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        var buttons = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalAlignment = HAlignment.Center };
        var again = new Button { Text = Loc.GetString("pda-arcade-again"), Margin = new Thickness(4) };
        again.OnPressed += _ =>
        {
            HideOverlay();
            _board?.Restart();
            _score.Text = Loc.GetString("pda-arcade-score", ("score", 0));
            GrabKeyboardFocus();
        };
        _finishButton = new Button { Text = Loc.GetString("pda-arcade-finish"), Margin = new Thickness(4) };
        _finishButton.OnPressed += _ => _board?.FinishNow();
        var menu = new Button { Text = Loc.GetString("pda-arcade-back"), Margin = new Thickness(4) };
        menu.OnPressed += _ => LeaveGame();
        buttons.AddChild(again);
        buttons.AddChild(_finishButton);
        buttons.AddChild(menu);
        box.AddChild(_overlayTitle);
        box.AddChild(_overlayRows);
        box.AddChild(buttons);
        panel.AddChild(box);
        return panel;
    }

    private void Tap(string command)
    {
        if (_board is PdaRocksBoard rocks)
        {
            if (command == "left")
                rocks.NudgeAngle(-0.35f);
            else if (command == "right")
                rocks.NudgeAngle(0.35f);
            else if (command == "up")
                rocks.Kick();
            else if (command == "act")
                rocks.Fire();
        }
        else
        {
            _board?.Command(command, true);
        }

        GrabKeyboardFocus();
    }

    private void RouteKey(GUIBoundKeyEventArgs args, bool down)
    {
        string? command = null;
        if (args.Function == ContentKeyFunctions.ArcadeLeft || args.Function == EngineKeyFunctions.MoveLeft || args.Function == EngineKeyFunctions.TextCursorLeft)
            command = "left";
        else if (args.Function == ContentKeyFunctions.ArcadeRight || args.Function == EngineKeyFunctions.MoveRight || args.Function == EngineKeyFunctions.TextCursorRight)
            command = "right";
        else if (args.Function == ContentKeyFunctions.ArcadeUp || args.Function == EngineKeyFunctions.MoveUp || args.Function == EngineKeyFunctions.TextCursorUp)
            command = "up";
        else if (args.Function == ContentKeyFunctions.ArcadeDown || args.Function == EngineKeyFunctions.MoveDown || args.Function == EngineKeyFunctions.TextCursorDown)
            command = "down";
        else if (args.Function == ContentKeyFunctions.Arcade1 || args.Function == EngineKeyFunctions.Use)
            command = "act";

        if (command == null)
            return;

        if (_match.Visible)
        {
            args.Handle();
            _table.Steer(command, down);
            return;
        }

        if (!_play.Visible || _board == null || _overlay.Visible)
            return;

        args.Handle();
        _board.Command(command, down);
    }

    private void ToggleSide()
    {
        var quiet = PdaArcadeArt.TryGame(_game, out var game) && game.Quiet;
        if (!quiet && _board != null)
            _board.IsPaused = !_board.IsPaused;

        if (_overlay.Visible && (_board == null || !_board.IsOver))
        {
            HideOverlay();
            return;
        }

        ShowGameBoard(quiet ? Loc.GetString("pda-arcade-board") : Loc.GetString("pda-arcade-pause"));
    }

    private void ShowGameBoard(string title)
    {
        _overlayTitle.Text = title;
        FillRows(_overlayRows, _game, 3);
        _finishButton.Visible = _board != null && !_board.IsOver;
        _overlay.Visible = true;
        _host.Visible = false;
    }

    private void HideOverlay()
    {
        _overlay.Visible = false;
        _host.Visible = true;
        if (_board != null)
            _board.IsPaused = false;
    }

    private void LeaveGame()
    {
        if (_board != null && !_board.IsOver)
            ScoreReady?.Invoke(_game, _board.Bank());

        ShowMenu();
    }

    private void ShowMenu()
    {
        _board?.Orphan();
        _board = null;
        _play.Visible = false;
        _scores.Visible = false;
        _match.Visible = false;
        _solo.Visible = true;
        _coop.Visible = false;
        _menu.Visible = true;
    }

    private void ShowScores()
    {
        _board?.Orphan();
        _board = null;
        _menu.Visible = false;
        _play.Visible = false;
        _coop.Visible = false;
        _scores.Visible = true;
        FillScores();
    }

    private void FillScores()
    {
        _scoreList.RemoveAllChildren();
        var any = false;
        foreach (var game in PdaArcadeArt.Games())
        {
            if (!HasScores(game.ID))
                continue;

            any = true;
            var title = new Label
            {
                Text = Loc.GetString(game.Name),
                Margin = new Thickness(8, 8, 8, 2),
            };
            _scoreList.AddChild(title);
            var rows = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(8, 0, 8, 4) };
            FillRows(rows, game.ID, 3);
            _scoreList.AddChild(rows);
        }

        if (!any)
            _scoreList.AddChild(new Label { Text = Loc.GetString("pda-arcade-empty"), Margin = new Thickness(8) });
    }

    private void FillRows(BoxContainer parent, string game, int count)
    {
        parent.RemoveAllChildren();
        var rows = new List<PdaArcadeEntry>();
        foreach (var entry in _entries)
        {
            if (entry.Game == game)
                rows.Add(entry);
        }

        rows.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (rows.Count == 0)
            return;

        var shown = Math.Min(count, rows.Count);
        for (var i = 0; i < shown; i++)
        {
            var color = i switch
            {
                0 => Color.FromHex("#f2d36b"),
                1 => Color.FromHex("#d7dde8"),
                _ => Color.FromHex("#d08b5b"),
            };
            var card = new PanelContainer
            {
                Margin = new Thickness(0, 2),
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#242838"), BorderColor = color, BorderThickness = new Thickness(2) },
            };
            card.AddChild(new Label
            {
                Text = $"{i + 1}. {rows[i].Name}  —  {rows[i].Score}",
                FontColorOverride = color,
                Margin = new Thickness(8, 4),
            });
            parent.AddChild(card);
        }
    }

    private bool HasScores(string game)
    {
        foreach (var entry in _entries)
        {
            if (entry.Game == game)
                return true;
        }

        return false;
    }

    private static Button Arrow(string text, Action action)
    {
        var button = new Button { Text = text, MinSize = new System.Numerics.Vector2(36, 32), Margin = new Thickness(2) };
        button.OnPressed += _ => action();
        return button;
    }
}
