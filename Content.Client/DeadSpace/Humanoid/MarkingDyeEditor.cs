// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.Humanoid;

public sealed class MarkingDyeEditor : BoxContainer
{
    private MarkingDye _dye = new();
    private bool _updating;

    private readonly CheckBox _enabledCheck;
    private readonly BoxContainer _settings;
    private readonly OptionButton _styleOption;
    private readonly Label _colorLabel;
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

        _colorLabel = new Label();
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
        _settings.AddChild(_colorLabel);
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
        _colorSelector.OnColorChanged += color =>
        {
            if (_updating)
                return;
            _dye.Color = color;
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
            _dye = value;
        UpdateControls();
        _updating = false;
    }

    private void Raise()
    {
        UpdateControls();
        OnDyeChanged?.Invoke(_enabledCheck.Pressed ? _dye : null);
    }

    private void UpdateControls()
    {
        _settings.Visible = _enabledCheck.Pressed;
        _styleOption.SelectId((int) _dye.Style);
        if (_colorSelector.Color != _dye.Color)
            _colorSelector.Color = _dye.Color;
        _amountSlider.SetValueWithoutEvent(_dye.Offset);
        _colorLabel.Text = Loc.GetString(StyleKey("color", _dye.Style));
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
