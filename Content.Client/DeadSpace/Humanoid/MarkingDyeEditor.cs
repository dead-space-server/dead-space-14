// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.Humanoid;

public sealed class MarkingDyeEditor : BoxContainer
{
    private MarkingDye _dye = new();
    private int _colorIndex;
    private bool _updating;

    private readonly CheckBox _enabledCheck;
    private readonly BoxContainer _settings;
    private readonly OptionButton _styleOption;
    private readonly OptionButton _colorOption;
    private readonly Button _addColorButton;
    private readonly Button _removeColorButton;
    private readonly ColorSelectorSliders _colorSelector;
    private readonly Label _amountLabel;
    private readonly Button _resetButton;
    private readonly Slider _amountSlider;

    public event Action<MarkingDye?>? OnDyeChanged;

    public MarkingDyeEditor()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        _enabledCheck = new CheckBox
        {
            Text = Loc.GetString("marking-dye-enabled"),
            HorizontalExpand = true,
        };

        _settings = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Visible = false,
        };

        _styleOption = new OptionButton { HorizontalExpand = true };
        foreach (var style in Enum.GetValues<MarkingDyeStyle>())
        {
            _styleOption.AddItem(Loc.GetString(StyleKey("style", style)), (int) style);
        }

        _colorOption = new OptionButton { HorizontalExpand = true };
        _addColorButton = new Button
        {
            Text = Loc.GetString("marking-dye-color-add"),
            ToolTip = Loc.GetString("marking-dye-color-add-tooltip"),
        };
        _removeColorButton = new Button
        {
            Text = Loc.GetString("marking-dye-color-remove"),
            ToolTip = Loc.GetString("marking-dye-color-remove-tooltip"),
        };

        var colorRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
        colorRow.AddChild(_colorOption);
        colorRow.AddChild(_addColorButton);
        colorRow.AddChild(_removeColorButton);

        _colorSelector = new ColorSelectorSliders { HorizontalExpand = true };
        _colorSelector.SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv;

        _amountLabel = new Label { HorizontalExpand = true };
        _resetButton = new Button { Text = Loc.GetString("marking-dye-reset") };

        var amountRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
        amountRow.AddChild(_amountLabel);
        amountRow.AddChild(_resetButton);

        _amountSlider = new Slider
        {
            MinValue = 0,
            MaxValue = 1,
            HorizontalExpand = true,
        };

        _settings.AddChild(_styleOption);
        _settings.AddChild(colorRow);
        _settings.AddChild(_colorSelector);
        _settings.AddChild(amountRow);
        _settings.AddChild(_amountSlider);

        AddChild(_enabledCheck);
        AddChild(_settings);

        _enabledCheck.OnToggled += _ => Raise();
        _resetButton.OnPressed += _ =>
        {
            _dye.Offset = new MarkingDye().Offset;
            Raise();
        };
        _styleOption.OnItemSelected += args =>
        {
            _styleOption.SelectId(args.Id);
            _dye.Style = (MarkingDyeStyle) args.Id;
            Raise();
        };
        _colorOption.OnItemSelected += args =>
        {
            _colorOption.SelectId(args.Id);
            _colorIndex = args.Id;
            UpdateControls();
        };
        _addColorButton.OnPressed += _ =>
        {
            if (_dye.Colors.Count >= MarkingDye.MaxColors)
                return;

            _dye.Colors.Add(_dye.Colors[^1]);
            _colorIndex = _dye.Colors.Count - 1;
            Raise();
        };
        _removeColorButton.OnPressed += _ =>
        {
            if (_dye.Colors.Count <= 1)
                return;

            _dye.Colors.RemoveAt(_colorIndex);
            _colorIndex = Math.Clamp(_colorIndex, 0, _dye.Colors.Count - 1);
            Raise();
        };
        _colorSelector.OnColorChanged += color =>
        {
            if (_updating)
                return;
            if (_colorIndex < _dye.Colors.Count)
                _dye.Colors[_colorIndex] = color;
            Raise();
        };
        _amountSlider.OnValueChanged += range =>
        {
            if (_updating)
                return;
            _dye.Offset = range.Value;
            Raise();
        };

        UpdateControls();
    }

    public void SetData(MarkingDye? dye, Color baseColor)
    {
        _updating = true;
        _enabledCheck.Pressed = dye is not null;
        if (dye is { } value)
            _dye = value with { Colors = new List<Color>(value.Colors) };
        _colorIndex = Math.Clamp(_colorIndex, 0, _dye.Colors.Count - 1);
        UpdateControls();
        _updating = false;
    }

    private void Raise()
    {
        UpdateControls();
        MarkingDye? dye = null;
        if (_enabledCheck.Pressed)
            dye = _dye with { Colors = new List<Color>(_dye.Colors) };
        OnDyeChanged?.Invoke(dye);
    }

    private void UpdateControls()
    {
        _settings.Visible = _enabledCheck.Pressed;
        _styleOption.SelectId((int) _dye.Style);

        _colorOption.Clear();
        for (var i = 0; i < _dye.Colors.Count; i++)
        {
            _colorOption.AddItem(Loc.GetString("marking-dye-color-entry", ("value", i + 1)), i);
        }
        _colorOption.SelectId(Math.Clamp(_colorIndex, 0, _dye.Colors.Count - 1));
        _removeColorButton.Disabled = _dye.Colors.Count <= 1;
        _addColorButton.Disabled = _dye.Colors.Count >= MarkingDye.MaxColors;

        if (_colorIndex >= 0 && _colorIndex < _dye.Colors.Count)
        {
            var color = _dye.Colors[_colorIndex];
            if (_colorSelector.Color != color)
                _colorSelector.Color = color;
        }

        _amountSlider.SetValueWithoutEvent(_dye.Offset);
        _amountLabel.Text = Loc.GetString(StyleKey("amount", _dye.Style), ("value", (int) MathF.Round(_dye.Offset * 100f)));
    }

    public static Color BaseColor(Marking marking)
    {
        return marking.MarkingColors.Count > 0 ? marking.MarkingColors[0] : Color.White;
    }

    private static string StyleKey(string kind, MarkingDyeStyle style)
    {
        return $"marking-dye-{kind}-{style.ToString().ToLowerInvariant()}";
    }
}
