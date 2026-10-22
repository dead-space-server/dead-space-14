// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiColorRow : BoxContainer
{
    public event Action? UserChanged;
    public event Action? Eyedropper;

    private readonly PanelContainer _swatch;
    private readonly StyleBoxFlat _swatchBox;
    private readonly LineEdit _hex;
    private readonly ColorSelectorSliders _sliders;
    private readonly CheckBox? _custom;
    private bool _suppress;

    public UiColorRow(string title, bool optional)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        row.AddChild(new Label
        {
            Text = title,
            MinSize = new System.Numerics.Vector2(118, 0),
            VerticalAlignment = Control.VAlignment.Center,
        });

        _sliders = new ColorSelectorSliders
        {
            Visible = false,
            HorizontalExpand = true,
            IsAlphaVisible = true,
            SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv,
        };
        _swatchBox = new StyleBoxFlat(Color.White);
        _swatch = new PanelContainer
        {
            PanelOverride = _swatchBox,
            MouseFilter = MouseFilterMode.Stop,
            MinSize = new System.Numerics.Vector2(36, 22),
            VerticalAlignment = Control.VAlignment.Center,
        };
        _swatch.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            _sliders.Visible = !_sliders.Visible;
            if (optional && _custom is { Pressed: false })
            {
                _suppress = true;
                _custom.Pressed = true;
                _suppress = false;
                UserChanged?.Invoke();
            }
        };
        row.AddChild(_swatch);

        _hex = new LineEdit { MinSize = new System.Numerics.Vector2(96, 0), HorizontalExpand = true };
        _hex.OnTextEntered += _ => CommitHex();
        _hex.OnFocusExit += _ => CommitHex();
        row.AddChild(_hex);

        var pick = new Button { Text = "Пипетка" };
        pick.OnPressed += _ => Eyedropper?.Invoke();
        row.AddChild(pick);

        if (optional)
        {
            _custom = new CheckBox { Text = "Свой" };
            _custom.OnToggled += _ =>
            {
                if (!_suppress)
                    UserChanged?.Invoke();
            };
            row.AddChild(_custom);
        }
        else
        {
            _custom = null;
        }

        AddChild(row);
        _sliders.OnColorChanged += color =>
        {
            if (_suppress)
                return;

            SetColor(color, true, notify: false);
            UserChanged?.Invoke();
        };
        AddChild(_sliders);
    }

    public Color Color { get; private set; } = Color.White;

    public bool Custom => _custom == null || _custom.Pressed;

    public void SetColor(Color color, bool custom)
    {
        SetColor(color, custom, notify: false);
    }

    public void ApplyPicked(Color color)
    {
        SetColor(color, true, notify: false);
        UserChanged?.Invoke();
    }

    private void SetColor(Color color, bool custom, bool notify)
    {
        _suppress = true;
        Color = color;
        _swatchBox.BackgroundColor = color;
        _hex.Text = UiLayoutFile.Hex(color);
        _sliders.Color = color;
        if (_custom != null)
            _custom.Pressed = custom;
        _suppress = false;
        if (notify)
            UserChanged?.Invoke();
    }

    private void CommitHex()
    {
        if (_suppress)
            return;

        var parsed = Color.TryFromHex(_hex.Text);
        if (parsed == null)
            return;

        SetColor(parsed.Value, true, notify: false);
        UserChanged?.Invoke();
    }
}
