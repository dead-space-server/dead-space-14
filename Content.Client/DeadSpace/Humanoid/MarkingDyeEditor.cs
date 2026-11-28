// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.Humanoid;

public sealed class MarkingDyeEditor : BoxContainer
{
    private readonly CheckBox _toggle;
    private readonly BoxContainer _panel;
    private readonly OptionButton _style;
    private readonly OptionButton _swatch;
    private readonly ColorSelectorSliders _picker;
    private readonly Slider _share;
    private readonly Label _shareLabel;
    private readonly Button _add;
    private readonly Button _remove;
    private readonly Button _reset;

    private MarkingDye _dye = new();
    private Color _base = Color.White;
    private int _current;
    private bool _silent;

    public event Action<MarkingDye?>? OnDyeChanged;

    public MarkingDyeEditor()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 4;

        _toggle = new CheckBox
        {
            Text = Loc.GetString("marking-dye-enabled"),
            HorizontalExpand = true,
        };

        _panel = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Visible = false,
        };

        _style = new OptionButton { HorizontalExpand = true };
        foreach (var style in Enum.GetValues<MarkingDyeStyle>())
            _style.AddItem(Loc.GetString(Key("style", style)), (int) style);

        _swatch = new OptionButton { HorizontalExpand = true };
        _add = new Button
        {
            Text = Loc.GetString("marking-dye-color-add"),
            ToolTip = Loc.GetString("marking-dye-color-add-tooltip"),
        };
        _remove = new Button
        {
            Text = Loc.GetString("marking-dye-color-remove"),
            ToolTip = Loc.GetString("marking-dye-color-remove-tooltip"),
        };

        _picker = new ColorSelectorSliders
        {
            HorizontalExpand = true,
            SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv,
        };

        _shareLabel = new Label { HorizontalExpand = true };
        _reset = new Button { Text = Loc.GetString("marking-dye-reset") };

        _share = new Slider
        {
            MinValue = 0,
            MaxValue = 1,
            HorizontalExpand = true,
        };

        AddChild(_toggle);
        AddChild(_panel);

        _panel.AddChild(_style);
        _panel.AddChild(PaletteRow());
        _panel.AddChild(_picker);
        _panel.AddChild(ShareRow());
        _panel.AddChild(_share);

        LinkEvents();
        Refresh();
    }

    public void SetData(MarkingDye? dye, Color baseColor)
    {
        _silent = true;
        _base = baseColor;
        _toggle.Pressed = dye is not null;

        if (dye is { } value)
            _dye = value.Cleaned();

        _current = Math.Clamp(_current, 0, _dye.Colors.Count - 1);
        Refresh();
        _silent = false;
    }

    public static Color BaseColor(Marking marking)
    {
        return marking.MarkingColors.Count > 0 ? marking.MarkingColors[0] : Color.White;
    }

    private BoxContainer PaletteRow()
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        row.AddChild(_swatch);
        row.AddChild(_add);
        row.AddChild(_remove);
        return row;
    }

    private BoxContainer ShareRow()
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        row.AddChild(_shareLabel);
        row.AddChild(_reset);
        return row;
    }

    private void LinkEvents()
    {
        _toggle.OnToggled += _ =>
        {
            if (_toggle.Pressed && _dye.Colors is [{ } only] && only == Color.White)
                _dye.Colors[0] = _base;

            Publish();
        };

        _style.OnItemSelected += args =>
        {
            _style.SelectId(args.Id);
            _dye.Style = (MarkingDyeStyle) args.Id;
            Publish();
        };

        _swatch.OnItemSelected += args =>
        {
            _swatch.SelectId(args.Id);
            _current = args.Id;
            Refresh();
        };

        _add.OnPressed += _ =>
        {
            if (_dye.Colors.Count >= MarkingDye.MaxColors)
                return;

            _dye.Colors.Add(_dye.Colors[^1]);
            _current = _dye.Colors.Count - 1;
            Publish();
        };

        _remove.OnPressed += _ =>
        {
            if (_dye.Colors.Count <= 1)
                return;

            _dye.Colors.RemoveAt(_current);
            _current = Math.Clamp(_current, 0, _dye.Colors.Count - 1);
            Publish();
        };

        _picker.OnColorChanged += color =>
        {
            if (_silent || _current >= _dye.Colors.Count)
                return;

            _dye.Colors[_current] = color;
            Publish();
        };

        _share.OnValueChanged += range =>
        {
            if (_silent)
                return;

            _dye.Offset = range.Value;
            Publish();
        };

        _reset.OnPressed += _ =>
        {
            _dye.Offset = MarkingDye.DefaultOffset;
            Publish();
        };
    }

    private void Publish()
    {
        Refresh();

        if (!_toggle.Pressed)
        {
            OnDyeChanged?.Invoke(null);
            return;
        }

        OnDyeChanged?.Invoke(_dye with { Colors = new List<Color>(_dye.Colors) });
    }

    private void Refresh()
    {
        _panel.Visible = _toggle.Pressed;
        _style.SelectId((int) _dye.Style);

        _swatch.Clear();
        for (var i = 0; i < _dye.Colors.Count; i++)
            _swatch.AddItem(Loc.GetString("marking-dye-color-entry", ("value", i + 1)), i);

        var selected = Math.Clamp(_current, 0, _dye.Colors.Count - 1);
        _swatch.SelectId(selected);
        _add.Disabled = _dye.Colors.Count >= MarkingDye.MaxColors;
        _remove.Disabled = _dye.Colors.Count <= 1;

        var color = _dye.Colors[selected];
        if (_picker.Color != color)
            _picker.Color = color;

        _share.SetValueWithoutEvent(_dye.Offset);
        _shareLabel.Text = Loc.GetString(Key("amount", _dye.Style),
            ("value", (int) MathF.Round(_dye.Offset * 100f)));
    }

    private static string Key(string kind, MarkingDyeStyle style)
    {
        return $"marking-dye-{kind}-{style.ToString().ToLowerInvariant()}";
    }
}
