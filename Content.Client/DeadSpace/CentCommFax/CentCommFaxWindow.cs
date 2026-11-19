// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.DeadSpace.CentCommFax;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.CentCommFax;

public sealed class CentCommFaxWindow : DefaultWindow
{
    private const string DefaultStampState = "paper_stamp-centcom";

    private static readonly PhotocopierFormCategory[] TemplateCategories =
    {
        PhotocopierFormCategory.NTCentcomm,
        PhotocopierFormCategory.NTOperator,
    };

    private static readonly (string Loc, Color? Color)[] StampColors =
    {
        ("centcomm-fax-color-stamp", null),
        ("centcomm-fax-color-green", Color.FromHex("#006400")),
        ("centcomm-fax-color-red", Color.FromHex("#bb3232")),
        ("centcomm-fax-color-blue", Color.FromHex("#1f4e9c")),
        ("centcomm-fax-color-gold", Color.FromHex("#c9a227")),
        ("centcomm-fax-color-purple", Color.FromHex("#6b2d8f")),
        ("centcomm-fax-color-black", Color.FromHex("#222222")),
    };

    public event Action<CentCommFaxEuiMsg.Reply>? OnReply;
    public event Action<string, NetEntity?>? OnTemplateRequested;
    public event Action<NetEntity>? OnFollow;

    private readonly BoxContainer _inboxList;
    private readonly Label _inboxTitle;
    private readonly Label _viewTitle;
    private readonly Label _viewMeta;
    private readonly RichTextLabel _viewContent;
    private readonly Label _viewStamps;
    private readonly Button _followSender;
    private readonly OptionButton _targetSelector;
    private readonly OptionButton _templateSelector;
    private readonly LineEdit _titleEdit;
    private readonly LineEdit _fromEdit;
    private readonly TextEdit _contentEdit;
    private readonly OptionButton _stampSelector;
    private readonly OptionButton _colorSelector;
    private readonly CheckBox _lockBox;
    private readonly CheckBox _returnBox;
    private readonly Label _stampListLabel;
    private readonly Button _sendButton;
    private readonly Label _status;
    private readonly List<CentCommFaxStamp> _pendingStamps = new();

    private readonly ILocalizationManager _loc = IoCManager.Resolve<ILocalizationManager>();
    private readonly List<CentCommFaxTarget> _targets = new();
    private readonly List<string> _templates = new();
    private readonly List<string> _stamps = new();
    private readonly List<string> _stampNames = new();
    private List<CentCommFaxEntry> _inbox = new();
    private int _selected = -1;
    private int _targetIndex = -1;
    private string? _paperPrototype;

    public CentCommFaxWindow(IPrototypeManager prototypes, IComponentFactory factory)
    {
        Title = Loc.GetString("centcomm-fax-window-title");
        MinSize = new Vector2(1000, 600);
        SetSize = new Vector2(1180, 780);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 320,
            VerticalExpand = true,
        };
        _inboxTitle = new Label { StyleClasses = { "LabelHeading" } };
        left.AddChild(_inboxTitle);
        var inboxScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _inboxList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        inboxScroll.AddChild(_inboxList);
        left.AddChild(inboxScroll);
        root.AddChild(left);

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var viewHeader = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var viewTitles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _viewTitle = new Label { StyleClasses = { "LabelHeading" }, ClipText = true };
        _viewMeta = new Label { StyleClasses = { "LabelSubText" }, ClipText = true };
        viewTitles.AddChild(_viewTitle);
        viewTitles.AddChild(_viewMeta);
        viewHeader.AddChild(viewTitles);
        _followSender = new Button { Text = Loc.GetString("centcomm-fax-follow"), Disabled = true };
        _followSender.OnPressed += _ => FollowSelected();
        viewHeader.AddChild(_followSender);
        right.AddChild(viewHeader);

        var paper = new PanelContainer
        {
            MinHeight = 120,
            VerticalExpand = true,
            SizeFlagsStretchRatio = 1f,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#efe9da"),
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8,
            },
        };
        var paperScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _viewContent = new RichTextLabel { HorizontalExpand = true };
        paperScroll.AddChild(_viewContent);
        paper.AddChild(paperScroll);
        right.AddChild(paper);

        _viewStamps = new Label { StyleClasses = { "LabelSubText" }, ClipText = true };
        right.AddChild(_viewStamps);

        right.AddChild(new Label { Text = Loc.GetString("centcomm-fax-reply-header"), StyleClasses = { "LabelHeading" } });

        _returnBox = new CheckBox { Text = Loc.GetString("centcomm-fax-return-original"), Disabled = true };
        _returnBox.OnToggled += args => SetReturnOriginal(args.Pressed);
        right.AddChild(_returnBox);

        _targetSelector = new OptionButton { HorizontalExpand = true };
        _targetSelector.OnItemSelected += args =>
        {
            _targetSelector.SelectId(args.Id);
            _targetIndex = args.Id;
        };
        right.AddChild(MakeRow("centcomm-fax-target", _targetSelector));

        _templateSelector = new OptionButton { HorizontalExpand = true };
        _templateSelector.OnItemSelected += args => SelectTemplate(args.Id);
        right.AddChild(MakeRow("centcomm-fax-template", _templateSelector));

        _titleEdit = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("centcomm-fax-title-placeholder") };
        right.AddChild(MakeRow("centcomm-fax-title", _titleEdit));

        _fromEdit = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("centcomm-fax-from-placeholder") };
        right.AddChild(MakeRow("centcomm-fax-from", _fromEdit));

        _contentEdit = new TextEdit { HorizontalExpand = true, MinHeight = 120, VerticalExpand = true, SizeFlagsStretchRatio = 1f };
        _contentEdit.Placeholder = new Rope.Leaf(Loc.GetString("centcomm-fax-content-placeholder"));
        right.AddChild(_contentEdit);

        var stampRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _stampSelector = new OptionButton { HorizontalExpand = true };
        _stampSelector.OnItemSelected += args => _stampSelector.SelectId(args.Id);
        _colorSelector = new OptionButton { MinWidth = 170 };
        _colorSelector.OnItemSelected += args => _colorSelector.SelectId(args.Id);
        _lockBox = new CheckBox { Text = Loc.GetString("centcomm-fax-lock"), Pressed = true };
        stampRow.AddChild(new Label { Text = Loc.GetString("centcomm-fax-stamp"), MinWidth = 110 });
        stampRow.AddChild(_stampSelector);
        stampRow.AddChild(_colorSelector);
        stampRow.AddChild(_lockBox);
        right.AddChild(stampRow);

        var stampListRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var addStamp = new Button { Text = Loc.GetString("centcomm-fax-stamp-add") };
        addStamp.OnPressed += _ => AddPendingStamp();
        var clearStamps = new Button { Text = Loc.GetString("centcomm-fax-stamp-clear") };
        clearStamps.OnPressed += _ =>
        {
            _pendingStamps.Clear();
            UpdateStampList();
        };
        _stampListLabel = new Label { HorizontalExpand = true, StyleClasses = { "LabelSubText" }, ClipText = true };
        stampListRow.AddChild(new Label { MinWidth = 110 });
        stampListRow.AddChild(addStamp);
        stampListRow.AddChild(clearStamps);
        stampListRow.AddChild(_stampListLabel);
        right.AddChild(stampListRow);

        var sendRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _status = new Label { HorizontalExpand = true, StyleClasses = { "LabelSubText" } };
        _sendButton = new Button { Text = Loc.GetString("centcomm-fax-send"), MinWidth = 180 };
        _sendButton.OnPressed += _ => Send();
        sendRow.AddChild(_status);
        sendRow.AddChild(_sendButton);
        right.AddChild(sendRow);

        root.AddChild(right);
        Contents.AddChild(root);

        PopulateTemplates(prototypes);
        PopulateStamps(prototypes, factory);
        for (var i = 0; i < StampColors.Length; i++)
        {
            _colorSelector.AddItem(Loc.GetString(StampColors[i].Loc), i);
        }

        ShowEntry(null);
        UpdateStampList();
    }

    public void UpdateState(CentCommFaxEuiState state)
    {
        NetEntity? previousTarget = _targetIndex >= 0 && _targetIndex < _targets.Count ? _targets[_targetIndex].Fax : null;
        _targetIndex = -1;
        _targets.Clear();
        _targets.AddRange(state.Faxes.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));
        _targetSelector.Clear();
        for (var i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            var label = string.IsNullOrEmpty(target.Station) ? target.Name : $"{target.Name} ({target.Station})";
            _targetSelector.AddItem(label, i);
            _targetSelector.SetItemMetadata(i, target.Fax);
        }

        if (previousTarget != null)
            SelectTarget(previousTarget.Value);

        if (_targetIndex < 0 && _targets.Count > 0)
        {
            _targetSelector.SelectId(0);
            _targetIndex = 0;
        }

        var selectedId = _selected >= 0 && _selected < _inbox.Count ? _inbox[_selected].Id : -1;
        _inbox = state.Inbox.OrderByDescending(e => e.Id).ToList();
        _selected = _inbox.FindIndex(e => e.Id == selectedId);

        _inboxTitle.Text = Loc.GetString("centcomm-fax-inbox", ("count", _inbox.Count));
        _inboxList.RemoveAllChildren();
        if (_inbox.Count == 0)
            _inboxList.AddChild(new Label { Text = Loc.GetString("centcomm-fax-inbox-empty"), StyleClasses = { "LabelSubText" } });

        for (var i = 0; i < _inbox.Count; i++)
        {
            var index = i;
            var entry = _inbox[i];
            var button = new Button
            {
                ToggleMode = true,
                Pressed = i == _selected,
                HorizontalExpand = true,
                ClipText = true,
                Text = Loc.GetString(entry.Replies > 0 ? "centcomm-fax-inbox-row-replied" : "centcomm-fax-inbox-row",
                    ("time", entry.Time),
                    ("sender", entry.Sender),
                    ("title", entry.Title)),
            };
            button.OnPressed += _ => SelectEntry(index);
            _inboxList.AddChild(button);
        }

        ShowEntry(_selected >= 0 ? _inbox[_selected] : null);
    }

    public void ApplyTemplate(CentCommFaxEuiMsg.TemplateLoaded template)
    {
        _titleEdit.Text = template.Title;
        _contentEdit.TextRope = new Rope.Leaf(template.Content);
        _paperPrototype = template.PaperPrototype;
        _status.Text = Loc.GetString("centcomm-fax-status-template", ("template", template.Title));
    }

    private void SelectEntry(int index)
    {
        _selected = index;
        for (var i = 0; i < _inboxList.ChildCount; i++)
        {
            if (_inboxList.GetChild(i) is Button button)
                button.Pressed = i == index;
        }

        var entry = _inbox[index];
        ShowEntry(entry);

        if (entry.SenderFax is { } sender)
            SelectTarget(sender);

        if (string.IsNullOrWhiteSpace(_titleEdit.Text))
            _titleEdit.Text = Loc.GetString("centcomm-fax-reply-title", ("title", entry.Title));
    }

    private void ShowEntry(CentCommFaxEntry? entry)
    {
        if (entry == null)
        {
            _viewTitle.Text = Loc.GetString("centcomm-fax-no-selection");
            _viewMeta.Text = string.Empty;
            _viewContent.SetMessage(string.Empty);
            _viewStamps.Text = string.Empty;
            _followSender.Disabled = true;
            _returnBox.Pressed = false;
            _returnBox.Disabled = true;
            SetReturnOriginal(false);
            return;
        }

        _returnBox.Disabled = false;
        if (_returnBox.Pressed && entry.SenderFax is { } senderFax)
            SelectTarget(senderFax);

        _viewTitle.Text = entry.Title;
        _viewMeta.Text = Loc.GetString("centcomm-fax-meta",
            ("sender", entry.Sender),
            ("station", string.IsNullOrEmpty(entry.Station) ? Loc.GetString("centcomm-fax-none") : entry.Station),
            ("time", entry.Time));
        _viewContent.SetMarkupPermissive($"[color=#1a1a1a]{entry.Content}[/color]");

        var stamps = entry.Stamps.Select(s => LocalizeOrRaw(s.StampedName)).ToList();
        _viewStamps.Text = Loc.GetString("centcomm-fax-stamps",
            ("stamps", stamps.Count == 0 ? Loc.GetString("centcomm-fax-none") : string.Join(", ", stamps)),
            ("signatures", entry.Signatures.Count == 0 ? Loc.GetString("centcomm-fax-none") : string.Join(", ", entry.Signatures)));
        _followSender.Disabled = entry.SenderFax == null;
    }

    private void SelectTarget(NetEntity fax)
    {
        var index = _targets.FindIndex(t => t.Fax == fax);
        if (index < 0)
            return;

        _targetSelector.SelectId(index);
        _targetIndex = index;
    }

    private void FollowSelected()
    {
        if (_selected >= 0 && _selected < _inbox.Count && _inbox[_selected].SenderFax is { } fax)
            OnFollow?.Invoke(fax);
    }

    private void PopulateTemplates(IPrototypeManager prototypes)
    {
        _templateSelector.AddItem(Loc.GetString("centcomm-fax-template-none"), 0);
        _templates.Add(string.Empty);

        var forms = prototypes.EnumeratePrototypes<PaperworkFormPrototype>()
            .Where(f => f.Listed && TemplateCategories.Contains(f.Category))
            .OrderBy(f => f.Category)
            .ThenBy(f => Loc.GetString(f.Name), StringComparer.CurrentCultureIgnoreCase);

        foreach (var form in forms)
        {
            _templateSelector.AddItem(Loc.GetString(form.Name), _templates.Count);
            _templates.Add(form.ID);
        }
    }

    private void SelectTemplate(int id)
    {
        _templateSelector.SelectId(id);
        if (id <= 0 || id >= _templates.Count)
        {
            _paperPrototype = null;
            return;
        }

        OnTemplateRequested?.Invoke(_templates[id], GetTarget()?.Fax);
    }

    private void PopulateStamps(IPrototypeManager prototypes, IComponentFactory factory)
    {
        _stampSelector.AddItem(Loc.GetString("centcomm-fax-stamp-none"), 0);
        _stamps.Add(string.Empty);
        _stampNames.Add(string.Empty);

        var stamps = new List<(string Id, string Name, string State)>();
        var seen = new HashSet<string>();
        foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract ||
                proto.HideSpawnMenu ||
                !proto.TryGetComponent<StampComponent>(out var stamp, factory) ||
                !seen.Add($"{proto.Name}|{stamp.StampState}"))
            {
                continue;
            }

            stamps.Add((proto.ID, proto.Name, stamp.StampState));
        }

        var defaultId = 0;
        foreach (var (id, name, state) in stamps.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var index = _stamps.Count;
            _stampSelector.AddItem(name, index);
            _stamps.Add(id);
            _stampNames.Add(name);

            if (defaultId == 0 && state == DefaultStampState)
                defaultId = index;
        }

        _stampSelector.SelectId(defaultId);
    }

    private string LocalizeOrRaw(string value)
    {
        return _loc.TryGetString(value, out var localized) ? localized : value;
    }

    /// <summary>The stamp and ink picked in the selectors, if a stamp is picked.</summary>
    private CentCommFaxStamp? SelectedStamp()
    {
        if (_stampSelector.SelectedId <= 0 || _stampSelector.SelectedId >= _stamps.Count)
            return null;

        return new CentCommFaxStamp(_stamps[_stampSelector.SelectedId],
            StampColors[Math.Clamp(_colorSelector.SelectedId, 0, StampColors.Length - 1)].Color);
    }

    private void AddPendingStamp()
    {
        if (SelectedStamp() is { } stamp)
        {
            _pendingStamps.Add(stamp);
            UpdateStampList();
        }
    }

    private void UpdateStampList()
    {
        var names = _pendingStamps.Select(s => _stampNames[Math.Max(0, _stamps.IndexOf(s.Prototype))]);
        _stampListLabel.Text = _pendingStamps.Count == 0
            ? Loc.GetString("centcomm-fax-stamp-list-empty")
            : Loc.GetString("centcomm-fax-stamp-list", ("stamps", string.Join(", ", names)));
    }

    /// <summary>Returning the received paper itself: its text is kept, only stamps and the receiver are picked.</summary>
    private void SetReturnOriginal(bool value)
    {
        _titleEdit.Editable = !value;
        _contentEdit.Editable = !value;
        _templateSelector.Disabled = value;

        if (value && _selected >= 0 && _selected < _inbox.Count && _inbox[_selected].SenderFax is { } senderFax)
            SelectTarget(senderFax);
    }

    private void Send()
    {
        if (GetTarget() is not { } target)
        {
            _status.Text = Loc.GetString("centcomm-fax-status-no-target");
            return;
        }

        var returnOriginal = _returnBox.Pressed && _selected >= 0 && _selected < _inbox.Count;
        var content = Rope.Collapse(_contentEdit.TextRope);
        if (!returnOriginal && (string.IsNullOrWhiteSpace(_titleEdit.Text) || string.IsNullOrWhiteSpace(content)))
        {
            _status.Text = Loc.GetString("centcomm-fax-status-empty");
            return;
        }

        // The list of added stamps, or just the one picked in the selector if none were added.
        var stamps = new List<CentCommFaxStamp>(_pendingStamps);
        if (stamps.Count == 0 && SelectedStamp() is { } single)
            stamps.Add(single);

        OnReply?.Invoke(new CentCommFaxEuiMsg.Reply
        {
            ReplyTo = _selected >= 0 && _selected < _inbox.Count ? _inbox[_selected].Id : -1,
            Target = target.Fax,
            Title = _titleEdit.Text,
            From = _fromEdit.Text,
            Content = content,
            Stamps = stamps,
            Locked = _lockBox.Pressed,
            PaperPrototype = _paperPrototype,
            ReturnOriginal = returnOriginal,
        });

        _status.Text = Loc.GetString("centcomm-fax-status-sent", ("target", target.Name));
        _pendingStamps.Clear();
        UpdateStampList();
        if (returnOriginal)
            return;

        _contentEdit.TextRope = new Rope.Leaf(string.Empty);
        _titleEdit.Text = string.Empty;
        _paperPrototype = null;
        _templateSelector.SelectId(0);
    }

    private CentCommFaxTarget? GetTarget()
    {
        return _targetIndex >= 0 && _targetIndex < _targets.Count ? _targets[_targetIndex] : null;
    }

    private static Control MakeRow(string label, Control control)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = Loc.GetString(label), MinWidth = 110 });
        row.AddChild(control);
        return row;
    }
}
