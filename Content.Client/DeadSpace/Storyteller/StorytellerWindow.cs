// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Storyteller;

public sealed class StorytellerWindow : DefaultWindow
{
    public bool Teller { get; private set; }

    private readonly StorytellerSystem _story;
    private readonly IPrototypeManager _prototypes;
    private readonly StoryStageControl _stage;
    private readonly Label _status = new() { Margin = new Thickness(8, 4, 8, 0) };
    private readonly TabContainer _tabs = new() { MinSize = new Vector2(0, 230) };
    private readonly Button _leave = new() { Margin = new Thickness(8, 4) };
    private readonly Button _stop = new() { Margin = new Thickness(8, 4) };

    private readonly LineEdit _line = new() { HorizontalExpand = true };
    private readonly Button _applyNow = new() { ToggleMode = true, Pressed = true };
    private readonly BoxContainer _suggestions = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _applyAll = new();
    private readonly List<StoryCue> _pending = new();

    private readonly OptionButton _locations = new() { MinSize = new Vector2(200, 0) };
    private const int MaxPalette = 48;

    private readonly OptionButton _light = new() { MinSize = new Vector2(140, 0) };
    private readonly OptionButton _tint = new() { MinSize = new Vector2(140, 0) };
    private readonly List<string> _lightIds = new();
    private readonly List<string> _tintIds = new();
    private readonly List<string> _propGroupIds = new();
    private readonly Button _editRoom = new() { ToggleMode = true };
    private readonly OptionButton _propGroups = new() { MinSize = new Vector2(170, 0) };
    private readonly LineEdit _propSearch = new() { MinSize = new Vector2(220, 0) };
    private readonly Label _brushLabel = new() { Margin = new Thickness(8, 0, 0, 0) };
    private readonly StoryFlowContainer _palette = new() { HorizontalExpand = true };
    private readonly IEntityManager _entities;
    private List<EntityPrototype>? _searchable;
    private string? _brush;
    private string _defaultLight = "";
    private string _defaultTint = "";
    private readonly LineEdit _name = new() { MinSize = new Vector2(170, 0) };
    private readonly OptionButton _kinds = new() { MinSize = new Vector2(150, 0) };
    private readonly OptionButton _sex = new() { MinSize = new Vector2(110, 0) };
    private readonly OptionButton _roles = new() { MinSize = new Vector2(170, 0) };
    private readonly Button _copyPlayer = new() { ToggleMode = true };

    private readonly OptionButton _actor = new() { MinSize = new Vector2(170, 0) };
    private readonly OptionButton _target = new() { MinSize = new Vector2(170, 0) };
    private readonly OptionButton _item = new() { MinSize = new Vector2(170, 0) };
    private readonly LineEdit _speech = new() { HorizontalExpand = true };

    private readonly LineEdit _title = new() { MinSize = new Vector2(260, 0) };
    private readonly Label _bookInfo = new();
    private readonly BoxContainer _scriptRow = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal };
    private readonly Label _scriptInfo = new() { Margin = new Thickness(0, 0, 8, 0) };
    private readonly BoxContainer _writeBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };

    private readonly List<string> _locationIds = new();
    private readonly List<string> _kindIds = new();
    private readonly List<string> _roleIds = new();
    private readonly List<string> _itemIds = new();
    private readonly List<StoryActorState> _actors = new();
    private string _tellerName = "";

    public StorytellerWindow(StorytellerSystem story, IEntityManager entities, IPrototypeManager prototypes)
    {
        _story = story;
        _prototypes = prototypes;
        _entities = entities;
        _stage = new StoryStageControl(entities, prototypes, story) { VerticalExpand = true, HorizontalExpand = true };

        Title = Loc.GetString("storyteller-window-title");
        MinSize = new Vector2(960, 560);
        SetSize = new Vector2(1060, 860);

        _leave.Text = Loc.GetString("storyteller-leave");
        _stop.Text = Loc.GetString("storyteller-stop");
        _leave.OnPressed += _ => Close();
        _stop.OnPressed += _ => Close();

        _tabs.AddChild(BuildStoryTab());
        _tabs.AddChild(BuildSceneTab());
        _tabs.AddChild(BuildActionsTab());
        _tabs.AddChild(BuildRoomTab());
        _tabs.AddChild(BuildBookTab());
        TabContainer.SetTabTitle(_tabs.GetChild(0), Loc.GetString("storyteller-tab-story"));
        TabContainer.SetTabTitle(_tabs.GetChild(1), Loc.GetString("storyteller-tab-scene"));
        TabContainer.SetTabTitle(_tabs.GetChild(2), Loc.GetString("storyteller-tab-actions"));
        TabContainer.SetTabTitle(_tabs.GetChild(3), Loc.GetString("storyteller-tab-room"));
        TabContainer.SetTabTitle(_tabs.GetChild(4), Loc.GetString("storyteller-tab-book"));
        _tabs.OnTabChanged += index =>
        {
            _stage.Editing = index == 3 && _editRoom.Pressed;
        };

        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        bottom.AddChild(_status);
        bottom.AddChild(new Control { HorizontalExpand = true });
        bottom.AddChild(_leave);
        bottom.AddChild(_stop);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_stage);
        root.AddChild(bottom);
        root.AddChild(_tabs);
        Contents.AddChild(root);

        SetTeller(false);
        ShowSuggestions(Array.Empty<string>(), Array.Empty<StoryCue>());
    }

    #region Tabs

    private Control BuildStoryTab()
    {
        _line.PlaceHolder = Loc.GetString("storyteller-line-placeholder");
        _line.OnTextEntered += _ => SendLine();
        _applyNow.Text = Loc.GetString("storyteller-apply-now");
        _applyAll.Text = Loc.GetString("storyteller-apply-all");
        _applyAll.OnPressed += _ =>
        {
            if (_pending.Count > 0)
                _story.SendCues(_pending.ToArray());
        };

        var say = new Button { Text = Loc.GetString("storyteller-send") };
        say.OnPressed += _ => SendLine();

        var input = Row();
        input.AddChild(_line);
        input.AddChild(say);
        input.AddChild(_applyNow);

        var tab = Column();
        tab.AddChild(input);
        tab.AddChild(new Label { Text = Loc.GetString("storyteller-suggestions"), Margin = new Thickness(0, 6, 0, 0) });
        tab.AddChild(_suggestions);
        tab.AddChild(_applyAll);
        return Scroll(tab);
    }

    private Control BuildSceneTab()
    {
        foreach (var location in _prototypes.EnumeratePrototypes<StoryLocationPrototype>().OrderBy(p => p.Order))
        {
            _locations.AddItem(Loc.GetString(location.Name), _locationIds.Count);
            _locationIds.Add(location.ID);
        }

        _locations.OnItemSelected += args => _locations.SelectId(args.Id);
        var go = new Button { Text = Loc.GetString("storyteller-go") };
        go.OnPressed += _ =>
        {
            if (_locations.SelectedId >= 0 && _locations.SelectedId < _locationIds.Count)
                _story.SendCues(new StoryCue { Kind = StoryCueKind.Location, Id = _locationIds[_locations.SelectedId] });
        };

        foreach (var kind in _prototypes.EnumeratePrototypes<StoryKindPrototype>().OrderBy(p => p.Order))
        {
            _kinds.AddItem(Loc.GetString(kind.Name), _kindIds.Count);
            _kindIds.Add(kind.ID);
        }

        _kinds.OnItemSelected += args => _kinds.SelectId(args.Id);
        _sex.AddItem(Loc.GetString("storyteller-sex-auto"), 0);
        _sex.AddItem(Loc.GetString("storyteller-sex-Male"), 1);
        _sex.AddItem(Loc.GetString("storyteller-sex-Female"), 2);
        _sex.OnItemSelected += args => _sex.SelectId(args.Id);

        _roles.AddItem(Loc.GetString("storyteller-role-none"), 0);
        _roleIds.Add("");
        foreach (var role in _prototypes.EnumeratePrototypes<StoryRolePrototype>().OrderBy(p => p.Order))
        {
            _roles.AddItem(Loc.GetString(role.Name), _roleIds.Count);
            _roleIds.Add(role.ID);
        }

        _roles.OnItemSelected += args => _roles.SelectId(args.Id);
        _name.PlaceHolder = Loc.GetString("storyteller-name-placeholder");
        _copyPlayer.Text = Loc.GetString("storyteller-copy-player");

        var enter = new Button { Text = Loc.GetString("storyteller-enter") };
        enter.OnPressed += _ => Enter(false);
        var self = new Button { Text = Loc.GetString("storyteller-enter-self") };
        self.OnPressed += _ => Enter(true);

        var placeRow = Flow();
        placeRow.AddChild(new Label { Text = Loc.GetString("storyteller-location") });
        placeRow.AddChild(_locations);
        placeRow.AddChild(go);

        foreach (var light in _prototypes.EnumeratePrototypes<StoryLightPrototype>().OrderBy(p => p.Order))
        {
            _light.AddItem(Loc.GetString(light.Name), _lightIds.Count);
            _lightIds.Add(light.ID);
            if (_defaultLight.Length == 0 && light.Darkness == null && light.Pulse.A <= 0f)
                _defaultLight = light.ID;
        }

        _light.OnItemSelected += args =>
        {
            _light.SelectId(args.Id);
            _story.SendCues(new StoryCue { Kind = StoryCueKind.Light, Id = _lightIds[args.Id] });
        };

        foreach (var tint in _prototypes.EnumeratePrototypes<StoryTintPrototype>().OrderBy(p => p.Order))
        {
            _tint.AddItem(Loc.GetString(tint.Name), _tintIds.Count);
            _tintIds.Add(tint.ID);
            if (_defaultTint.Length == 0 && tint.Color == null)
                _defaultTint = tint.ID;
        }

        _tint.OnItemSelected += args =>
        {
            _tint.SelectId(args.Id);
            _story.SendCues(new StoryCue { Kind = StoryCueKind.Light, Id = "tint-" + _tintIds[args.Id] });
        };

        placeRow.AddChild(new Label { Text = Loc.GetString("storyteller-light"), Margin = new Thickness(12, 0, 0, 0) });
        placeRow.AddChild(_light);
        placeRow.AddChild(new Label { Text = Loc.GetString("storyteller-tint") });
        placeRow.AddChild(_tint);

        var castRow = Flow();
        castRow.AddChild(_name);
        castRow.AddChild(_kinds);
        castRow.AddChild(_sex);
        castRow.AddChild(_roles);

        var castButtons = Flow();
        castButtons.AddChild(enter);
        castButtons.AddChild(self);
        castButtons.AddChild(_copyPlayer);

        var tab = Column();
        tab.AddChild(placeRow);
        tab.AddChild(new Label { Text = Loc.GetString("storyteller-cast"), Margin = new Thickness(0, 8, 0, 0) });
        tab.AddChild(castRow);
        tab.AddChild(castButtons);
        return Scroll(tab);
    }

    private Control BuildActionsTab()
    {
        _actor.OnItemSelected += args => _actor.SelectId(args.Id);
        _target.OnItemSelected += args => _target.SelectId(args.Id);
        _item.OnItemSelected += args => _item.SelectId(args.Id);

        _item.AddItem(Loc.GetString("storyteller-item-none"), 0);
        _itemIds.Add("");
        foreach (var item in _prototypes.EnumeratePrototypes<StoryItemPrototype>().OrderBy(p => p.Order))
        {
            _item.AddItem(Loc.GetString(item.Name), _itemIds.Count);
            _itemIds.Add(item.ID);
        }

        var who = Flow();
        who.AddChild(new Label { Text = Loc.GetString("storyteller-who") });
        who.AddChild(_actor);
        who.AddChild(new Label { Text = Loc.GetString("storyteller-whom") });
        who.AddChild(_target);
        who.AddChild(new Label { Text = Loc.GetString("storyteller-with") });
        who.AddChild(_item);

        var moves = Flow();
        AddButton(moves, "storyteller-move-Left", () => Cue(StoryCueKind.Move, move: StoryMove.Left));
        AddButton(moves, "storyteller-move-Right", () => Cue(StoryCueKind.Move, move: StoryMove.Right));
        AddButton(moves, "storyteller-move-Up", () => Cue(StoryCueKind.Move, move: StoryMove.Up));
        AddButton(moves, "storyteller-move-Down", () => Cue(StoryCueKind.Move, move: StoryMove.Down));
        AddButton(moves, "storyteller-move-Center", () => Cue(StoryCueKind.Move, move: StoryMove.Center));
        AddButton(moves, "storyteller-action-walk-to", () => Cue(StoryCueKind.Move, move: StoryMove.Target, needTarget: true));
        AddButton(moves, "storyteller-action-run-to", () => Cue(StoryCueKind.Move, move: StoryMove.Target, needTarget: true, run: true));
        AddButton(moves, "storyteller-action-leave", () => Cue(StoryCueKind.Leave));

        var poses = Flow();
        AddButton(poses, "storyteller-pose-Stand", () => Cue(StoryCueKind.Pose, pose: StoryPose.Stand));
        AddButton(poses, "storyteller-pose-Sit", () => Cue(StoryCueKind.Pose, pose: StoryPose.Sit));
        AddButton(poses, "storyteller-pose-Lie", () => Cue(StoryCueKind.Pose, pose: StoryPose.Lie));
        AddButton(poses, "storyteller-pose-Kneel", () => Cue(StoryCueKind.Pose, pose: StoryPose.Kneel));
        AddButton(poses, "storyteller-pose-Fallen", () => Cue(StoryCueKind.Pose, pose: StoryPose.Fallen));
        AddButton(poses, "storyteller-pose-Dead", () => Cue(StoryCueKind.Pose, pose: StoryPose.Dead));
        AddButton(poses, "storyteller-action-turn", Turn);
        AddButton(poses, "storyteller-action-face", () => Cue(StoryCueKind.Face, move: StoryMove.Target, needTarget: true));
        AddButton(poses, "storyteller-action-dance", () => Cue(StoryCueKind.Dance));

        var interactions = Flow();
        AddButton(interactions, "storyteller-action-hit", () => Cue(StoryCueKind.Hit, needTarget: true));
        AddButton(interactions, "storyteller-action-shoot", () => Cue(StoryCueKind.Shoot));
        AddButton(interactions, "storyteller-action-hug", () => Cue(StoryCueKind.Hug, needTarget: true));
        AddButton(interactions, "storyteller-action-kiss", () => Cue(StoryCueKind.Kiss, needTarget: true));
        AddButton(interactions, "storyteller-action-give", () => Cue(StoryCueKind.Give, needTarget: true));
        AddButton(interactions, "storyteller-action-hold", () => Cue(StoryCueKind.Hold, needItem: true));
        AddButton(interactions, "storyteller-action-point", () => Cue(StoryCueKind.Point));
        AddButton(interactions, "storyteller-action-shake", () => _story.SendCues(new StoryCue { Kind = StoryCueKind.Shake }));

        var emotes = Flow();
        foreach (var emote in _prototypes.EnumeratePrototypes<StoryEmotePrototype>().OrderBy(p => p.Order))
        {
            var id = emote.ID;
            AddButton(emotes, emote.Name, () => Cue(StoryCueKind.Emote, id: id));
        }

        _speech.PlaceHolder = Loc.GetString("storyteller-speech-placeholder");
        _speech.OnTextEntered += _ => Say();
        var sayButton = new Button { Text = Loc.GetString("storyteller-send") };
        sayButton.OnPressed += _ => Say();
        var speech = Row();
        speech.AddChild(_speech);
        speech.AddChild(sayButton);

        var camera = Flow();
        AddButton(camera, "storyteller-camera-in", () =>
        {
            var actor = Selected(_actor);
            _story.SendCues(new StoryCue { Kind = StoryCueKind.Camera, Run = true, Actor = actor?.Name ?? "" });
        });
        AddButton(camera, "storyteller-camera-out", () => _story.SendCues(new StoryCue { Kind = StoryCueKind.Camera }));

        var tab = Column();
        tab.AddChild(who);
        tab.AddChild(moves);
        tab.AddChild(poses);
        tab.AddChild(interactions);
        tab.AddChild(emotes);
        tab.AddChild(speech);
        tab.AddChild(camera);
        return Scroll(tab);
    }

    private Control BuildRoomTab()
    {
        _editRoom.Text = Loc.GetString("storyteller-room-edit");
        _editRoom.OnToggled += args => _stage.Editing = args.Pressed;

        foreach (var group in _prototypes.EnumeratePrototypes<StoryPropGroupPrototype>().OrderBy(p => p.Order))
        {
            _propGroups.AddItem(Loc.GetString(group.Name), _propGroupIds.Count);
            _propGroupIds.Add(group.ID);
        }

        _propGroups.OnItemSelected += args =>
        {
            _propGroups.SelectId(args.Id);
            _propSearch.Clear();
            FillPalette();
        };

        _propSearch.PlaceHolder = Loc.GetString("storyteller-room-search");
        _propSearch.OnTextChanged += _ => FillPalette();

        _stage.OnPlace += (x, y) =>
        {
            if (_brush != null)
                _story.SendCues(new StoryCue { Kind = StoryCueKind.PropAdd, Id = _brush, X = x, Y = y });
        };
        _stage.OnMoveProp += (id, x, y) => _story.SendCues(new StoryCue { Kind = StoryCueKind.PropMove, Prop = id, X = x, Y = y });
        _stage.OnRemoveProp += id => _story.SendCues(new StoryCue { Kind = StoryCueKind.PropRemove, Prop = id });
        _stage.OnSelectProp += _ => UpdateBrushLabel();

        var tools = Flow();
        tools.AddChild(_editRoom);
        AddButton(tools, "storyteller-room-turn", () =>
        {
            if (_stage.SelectedProp is { } id)
                _story.SendCues(new StoryCue { Kind = StoryCueKind.PropTurn, Prop = id });
        });
        AddButton(tools, "storyteller-room-remove", () =>
        {
            if (_stage.SelectedProp is { } id)
                _story.SendCues(new StoryCue { Kind = StoryCueKind.PropRemove, Prop = id });
        });
        AddButton(tools, "storyteller-room-clear", () => _story.SendCues(new StoryCue { Kind = StoryCueKind.PropClear }));
        AddButton(tools, "storyteller-room-reset", () => _story.SendCues(new StoryCue { Kind = StoryCueKind.PropReset }));

        var pick = Flow();
        pick.AddChild(new Label { Text = Loc.GetString("storyteller-room-group") });
        pick.AddChild(_propGroups);
        pick.AddChild(_propSearch);
        pick.AddChild(_brushLabel);

        var tab = Column();
        tab.AddChild(tools);
        tab.AddChild(pick);
        tab.AddChild(_palette);
        FillPalette();
        UpdateBrushLabel();
        return Scroll(tab);
    }

    private void FillPalette()
    {
        _palette.RemoveAllChildren();
        var query = _propSearch.Text.Trim();
        IEnumerable<EntityPrototype> protos;
        if (query.Length >= 2)
        {
            _searchable ??= _prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract && !p.HideSpawnMenu && p.Components.ContainsKey("Sprite"))
                .ToList();
            protos = _searchable
                .Where(p => p.ID.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(MaxPalette);
        }
        else
        {
            var index = _propGroups.SelectedId;
            if (index < 0 || index >= _propGroupIds.Count || !_prototypes.TryIndex<StoryPropGroupPrototype>(_propGroupIds[index], out var group))
                return;

            protos = group.Props
                .Select(id => _prototypes.TryIndex<EntityPrototype>(id, out var proto) ? proto : null)
                .OfType<EntityPrototype>()
                .Take(MaxPalette);
        }

        var sprites = _entities.System<SpriteSystem>();
        foreach (var proto in protos)
        {
            var id = proto.ID;
            var button = new Button
            {
                ToolTip = proto.Name,
                MinSize = new Vector2(150, 40),
                Margin = new Thickness(2),
                ToggleMode = true,
                Pressed = id == _brush,
            };
            var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            content.AddChild(new TextureRect
            {
                Texture = sprites.Frame0(proto),
                TextureScale = new Vector2(1f, 1f),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(32, 32),
            });
            content.AddChild(new Label { Text = proto.Name, ClipText = true, HorizontalExpand = true });
            button.AddChild(content);
            button.OnPressed += _ =>
            {
                if (_brush == id)
                {
                    _brush = null;
                    _stage.Brush = null;
                }
                else
                {
                    _brush = id;
                    _stage.Brush = id;
                    if (!_editRoom.Pressed)
                    {
                        _editRoom.Pressed = true;
                        _stage.Editing = true;
                    }
                }

                foreach (var child in _palette.Children)
                {
                    if (child is Button other)
                        other.Pressed = other == button && _brush != null;
                }

                UpdateBrushLabel();
            };
            _palette.AddChild(button);
        }
    }

    private void UpdateBrushLabel()
    {
        var brush = _brush != null && _prototypes.TryIndex<EntityPrototype>(_brush, out var proto) ? proto.Name : Loc.GetString("storyteller-room-no-brush");
        _brushLabel.Text = Loc.GetString("storyteller-room-brush", ("name", brush));
    }

    private Control BuildBookTab()
    {
        _title.PlaceHolder = Loc.GetString("storyteller-title-placeholder");
        var write = new Button { Text = Loc.GetString("storyteller-write-book") };
        write.OnPressed += _ => _story.SendWriteBook(_title.Text);

        var back = new Button { Text = Loc.GetString("storyteller-previous-page") };
        back.OnPressed += _ => _story.SendPage(false, true);
        var next = new Button { Text = Loc.GetString("storyteller-next-page") };
        next.OnPressed += _ => _story.SendPage(false);
        var restart = new Button { Text = Loc.GetString("storyteller-restart") };
        restart.OnPressed += _ => _story.SendPage(true);
        _scriptRow.AddChild(_scriptInfo);
        _scriptRow.AddChild(back);
        _scriptRow.AddChild(next);
        _scriptRow.AddChild(restart);

        var writeRow = Flow();
        writeRow.AddChild(_title);
        writeRow.AddChild(write);
        writeRow.AddChild(_bookInfo);
        _writeBox.AddChild(writeRow);

        var tab = Column();
        tab.AddChild(_writeBox);
        tab.AddChild(_scriptRow);
        return Scroll(tab);
    }

    #endregion

    public void SetTeller(bool teller)
    {
        Teller = teller;
        Title = teller
            ? Loc.GetString("storyteller-window-title-teller")
            : Loc.GetString("storyteller-window-title-listener", ("name", _tellerName));
        _tabs.Visible = teller;
        _stop.Visible = teller;
        _leave.Visible = !teller;
        if (!teller)
        {
            _editRoom.Pressed = false;
            _stage.Editing = false;
        }
    }

    public void SetScene(StorySceneEvent scene)
    {
        _tellerName = scene.TellerName;
        if (!Teller)
            Title = Loc.GetString("storyteller-window-title-listener", ("name", _tellerName));

        _stage.SetScene(scene);
        _status.Text = Loc.GetString("storyteller-listeners", ("count", scene.Listeners.Length));

        var actorId = Selected(_actor)?.Id;
        var targetId = Selected(_target)?.Id;
        _actors.Clear();
        _actors.AddRange(scene.Actors.Where(a => a.Pose != StoryPose.Gone));
        FillActors(_actor, actorId);
        FillActors(_target, targetId);
        if (_actors.Count > 1 && Selected(_target) is { } target && target.Id == Selected(_actor)?.Id)
            _target.SelectId(_actors.FindIndex(a => a.Id != target.Id));

        var lightIndex = _lightIds.IndexOf(scene.Light.Length == 0 ? _defaultLight : scene.Light);
        if (lightIndex >= 0)
            _light.SelectId(lightIndex);

        var tintIndex = _tintIds.IndexOf(scene.Tint.Length == 0 ? _defaultTint : scene.Tint);
        if (tintIndex >= 0)
            _tint.SelectId(tintIndex);

        var locationIndex = _locationIds.IndexOf(scene.Location);
        if (locationIndex >= 0)
            _locations.SelectId(locationIndex);


        _bookInfo.Text = Loc.GetString("storyteller-book-pages", ("count", scene.Pages));
        SetReading(scene.ScriptPages > 0);
        _scriptInfo.Text = Loc.GetString("storyteller-script-page",
            ("title", scene.Title), ("page", Math.Min(scene.ScriptPage + 1, scene.ScriptPages)), ("count", scene.ScriptPages));
        if (scene.ScriptPages > 0 && scene.ScriptPage >= scene.ScriptPages)
            _scriptInfo.Text = Loc.GetString("storyteller-script-done", ("title", scene.Title));
        if (scene.Title.Length > 0 && _title.Text.Length == 0)
            _title.Text = scene.Title;
    }

    public void ShowSuggestions(string[] labels, StoryCue[] cues)
    {
        _pending.Clear();
        _suggestions.RemoveAllChildren();
        if (labels.Length != cues.Length || labels.Length == 0)
        {
            _suggestions.AddChild(new Label { Text = Loc.GetString("storyteller-no-suggestions"), FontColorOverride = Color.Gray });
            _applyAll.Disabled = true;
            return;
        }

        for (var i = 0; i < Math.Min(labels.Length, StorytellerParser.MaxCues); i++)
        {
            var cue = cues[i];
            _pending.Add(cue);
            var button = new Button { Text = labels[i], Margin = new Thickness(1), ClipText = true, HorizontalExpand = true };
            button.OnPressed += _ => _story.SendCues(cue);
            _suggestions.AddChild(button);
        }

        _applyAll.Disabled = false;
    }

    private void FillActors(OptionButton button, byte? keep)
    {
        button.Clear();
        for (var i = 0; i < _actors.Count; i++)
            button.AddItem(_actors[i].Name, i);

        if (keep is { } id && _actors.FindIndex(a => a.Id == id) is var index and >= 0)
            button.SelectId(index);
    }

    private StoryActorState? Selected(OptionButton button)
    {
        var index = button.SelectedId;
        return button.ItemCount > 0 && index >= 0 && index < _actors.Count ? _actors[index] : null;
    }

    private void SendLine()
    {
        var line = _line.Text.Trim();
        if (line.Length == 0 || line.Length > StorytellerParser.MaxLineLength)
            return;

        _line.Clear();
        _story.SendLine(line, _applyNow.Pressed);
    }

    private void Say()
    {
        var actor = Selected(_actor);
        var text = _speech.Text.Trim();
        if (actor == null || text.Length == 0)
            return;

        if (text.Length > StorytellerParser.MaxSayLength)
            text = text[..StorytellerParser.MaxSayLength];

        _speech.Clear();
        _story.SendCues(new StoryCue { Kind = StoryCueKind.Say, Actor = actor.Name, Text = text });
    }

    private void Enter(bool self)
    {
        var name = _name.Text.Trim();
        if (name.Length > 0 && !StorytellerParser.CleanName(name, out name))
            return;

        var kind = _kinds.SelectedId >= 0 && _kinds.SelectedId < _kindIds.Count ? _kindIds[_kinds.SelectedId] : "";
        var role = _roles.SelectedId >= 0 && _roles.SelectedId < _roleIds.Count ? _roleIds[_roles.SelectedId] : "";
        var copy = _copyPlayer.Pressed && name.Length > 0;
        _story.SendCues(new StoryCue
        {
            Kind = StoryCueKind.Enter,
            Actor = self ? "" : name,
            Id = copy ? "" : kind,
            Role = copy ? "" : role,
            Sex = _sex.SelectedId == 2 ? Sex.Female : Sex.Male,
            HasSex = _sex.SelectedId != 0,
            Text = self ? "self" : "",
        });
        _name.Clear();
    }

    private void Cue(StoryCueKind kind, StoryMove move = StoryMove.None, StoryPose pose = StoryPose.Stand, string id = "",
        bool needTarget = false, bool needItem = false, bool run = false)
    {
        var actor = Selected(_actor);
        if (actor == null)
            return;

        var target = Selected(_target);
        if (target == actor)
            target = null;

        if (needTarget && target == null)
            return;

        var item = _item.SelectedId > 0 && _item.SelectedId < _itemIds.Count ? _itemIds[_item.SelectedId] : "";
        if (needItem && item.Length == 0)
            return;

        _story.SendCues(new StoryCue
        {
            Kind = kind,
            Actor = actor.Name,
            Target = target?.Name ?? "",
            Id = id.Length > 0 ? id : item,
            Move = move,
            Pose = pose,
            Run = run,
        });
    }

    private void SetReading(bool reading)
    {
        _scriptRow.Visible = reading;
        _writeBox.Visible = !reading;
        for (var i = 0; i < _tabs.ChildCount - 1; i++)
            TabContainer.SetTabVisible(_tabs.GetChild(i), !reading);

        if (!reading)
            return;

        _tabs.CurrentTab = _tabs.ChildCount - 1;
        _editRoom.Pressed = false;
        _stage.Editing = false;
    }

    private void Turn()
    {
        var actor = Selected(_actor);
        if (actor == null)
            return;

        var next = actor.Facing switch
        {
            Direction.South => StoryMove.Left,
            Direction.West => StoryMove.Up,
            Direction.North => StoryMove.Right,
            _ => StoryMove.Down,
        };

        _story.SendCues(new StoryCue { Kind = StoryCueKind.Face, Actor = actor.Name, Move = next });
    }

    private static void AddButton(Container parent, string loc, Action action)
    {
        var button = new Button { Text = Loc.GetString(loc), Margin = new Thickness(1) };
        button.OnPressed += _ => action();
        parent.AddChild(button);
    }

    private static BoxContainer Row()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(0, 3),
        };
    }

    private static BoxContainer Column()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };
    }

    private static StoryFlowContainer Flow()
    {
        return new StoryFlowContainer { HorizontalExpand = true, Margin = new Thickness(0, 3) };
    }

    private static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(content);
        return scroll;
    }
}
