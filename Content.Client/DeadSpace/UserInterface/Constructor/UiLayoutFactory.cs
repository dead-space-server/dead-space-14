// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiLayoutFactory
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    public UiLayoutFactory()
    {
        IoCManager.InjectDependencies(this);
    }

    public Control Create(UiLayoutElement element)
    {
        Control control = UiElementKinds.Get(element.Kind).CreateControl();

        Apply(control, element);
        return control;
    }

    public void Apply(Control control, UiLayoutElement element)
    {
        control.Visible = element.Visible;
        control.Modulate = element.Modulate;
        control.MinSize = new System.Numerics.Vector2(element.MinWidth, element.MinHeight);
        control.MaxSize = new System.Numerics.Vector2(
            element.MaxWidth <= 0 ? float.PositiveInfinity : element.MaxWidth,
            element.MaxHeight <= 0 ? float.PositiveInfinity : element.MaxHeight);
        control.Margin = MarginOf(element);
        control.HorizontalExpand = element.HorizontalExpand;
        control.VerticalExpand = element.VerticalExpand;
        if (element.SetWidth > 0)
            control.SetWidth = element.SetWidth;
        if (element.SetHeight > 0)
            control.SetHeight = element.SetHeight;
        control.HorizontalAlignment = element.HorizontalAlignment;
        control.VerticalAlignment = element.Height == UiHeightMode.Fill
            ? Control.VAlignment.Stretch
            : element.VerticalAlignment;
        control.StyleClasses.Clear();
        foreach (var styleClass in element.StyleClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            control.StyleClasses.Add(styleClass);

        switch (element.Kind)
        {
            case UiElementKind.Label when control is Label label:
                ApplyText(label, element);
                break;
            case UiElementKind.Button when control is Button button:
                button.Text = ShownText(element);
                button.Disabled = element.Disabled;
                button.ToggleMode = element.ToggleMode;
                if (element.ToggleMode)
                    button.Pressed = element.Pressed;
                ApplyText(button.Label, element);
                break;
            case UiElementKind.CheckBox when control is CheckBox check:
                check.Text = ShownText(element);
                check.Disabled = element.Disabled;
                check.Pressed = element.Pressed;
                ApplyText(check.Label, element);
                break;
            case UiElementKind.LineEdit when control is LineEdit edit:
                edit.Text = ShownText(element);
                edit.PlaceHolder = element.PlaceHolder;
                edit.Editable = element.Editable;
                break;
            case UiElementKind.Slider when control is Slider slider:
                slider.Disabled = element.Disabled;
                slider.MinValue = element.MinValue;
                slider.MaxValue = Math.Max(element.MinValue + 0.01f, element.MaxValue);
                slider.Value = element.Value;
                break;
            case UiElementKind.SpinBox when control is SpinBox spin:
                spin.Value = (int)Math.Round(element.Value);
                break;
            case UiElementKind.OptionButton when control is OptionButton option:
                option.Clear();
                var optionIndex = 0;
                foreach (var item in SplitItems(element.Items))
                {
                    option.AddItem(item, optionIndex);
                    optionIndex++;
                }

                if (optionIndex == 0)
                    option.AddItem(" ", 0);
                option.TrySelectId(Math.Clamp(element.Selected, 0, Math.Max(0, optionIndex - 1)));
                break;
            case UiElementKind.ItemList when control is ItemList list:
                list.Clear();
                foreach (var item in SplitItems(element.Items))
                    list.AddItem(item);
                break;
            case UiElementKind.ProgressBar when control is ProgressBar bar:
                bar.MinValue = element.MinValue;
                bar.MaxValue = Math.Max(element.MinValue + 0.01f, element.MaxValue);
                bar.Value = element.Value;
                if (element.ForegroundSet)
                    bar.ForegroundStyleBoxOverride = new StyleBoxFlat(element.Foreground);
                break;
            case UiElementKind.Panel when control is PanelContainer panel:
                panel.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = element.Background,
                    BorderColor = element.BorderColor,
                    BorderThickness = new Thickness(Math.Max(0, element.BorderThickness)),
                };
                break;
            case UiElementKind.Separator when control is PanelContainer line:
                line.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = element.Background,
                };
                break;
            case UiElementKind.TextureRect when control is TextureRect texture:
                texture.Stretch = element.Stretch;
                texture.Texture = ResolveTexture(element);
                break;
            case UiElementKind.Box when control is BoxContainer box:
                box.Orientation = element.Vertical
                    ? BoxContainer.LayoutOrientation.Vertical
                    : BoxContainer.LayoutOrientation.Horizontal;
                break;
            case UiElementKind.Grid when control is GridContainer grid:
                grid.Columns = Math.Max(1, element.Columns);
                break;
            case UiElementKind.SpriteView when control is SpriteView view:
                view.Scale = new System.Numerics.Vector2(element.ScaleX, element.ScaleY);
                if (Enum.TryParse<Direction>(element.Direction, out var direction))
                    view.OverrideDirection = direction;
                break;
        }
    }

    private string ShownText(UiLayoutElement element)
    {
        if (element.LocId.Length > 0 && _loc.TryGetString(element.LocId, out var value))
            return value;

        return element.Text;
    }

    private static Thickness MarginOf(UiLayoutElement element)
    {
        if (element.MarginLeft == 0 && element.MarginTop == 0 && element.MarginRight == 0 && element.MarginBottom == 0)
            return new Thickness(Math.Max(0, element.Margin));

        return new Thickness(element.MarginLeft, element.MarginTop, element.MarginRight, element.MarginBottom);
    }

    private Texture? ResolveTexture(UiLayoutElement element)
    {
        if (element.SpriteState.Length > 0 && element.TexturePath.Length > 0)
        {
            try
            {
                var rsi = _cache.GetResource<RSIResource>(element.TexturePath).RSI;
                if (rsi.TryGetState(new RSI.StateId(element.SpriteState), out var state))
                {
                    var direction = Math.Clamp(element.SpriteDirection, 0, state.Icons.Length - 1);
                    var frames = state.Icons[direction];
                    var frame = Math.Clamp(element.SpriteFrame, 0, frames.Length - 1);
                    return frames[frame];
                }
            }
            catch (Exception)
            {
                return element.LiveTexture;
            }
        }

        return element.LiveTexture ?? LoadTexture(element.TexturePath);
    }

    private void ApplyText(Label label, UiLayoutElement element)
    {
        label.Text = ShownText(element);
        label.Align = element.TextAlign;
        label.ClipText = element.ClipText;
        label.FontColorOverride = element.FontColorSet ? element.FontColor : null;
        label.FontOverride = LoadFont(element.FontId, element.FontSize);
    }

    private Font? LoadFont(string fontId, int size)
    {
        if (fontId.Length == 0 || !_prototypes.TryIndex<FontPrototype>(fontId, out var prototype))
            return null;

        try
        {
#pragma warning disable CS0618
            var path = prototype.Path;
#pragma warning restore CS0618
            return _cache.GetFont(path, Math.Clamp(size, 1, 128));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Texture? LoadTexture(string path)
    {
        if (path.Length == 0)
            return null;

        try
        {
            return _cache.GetTexture(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string[] SplitItems(string items)
    {
        if (items.Length == 0)
            return Array.Empty<string>();

        return items.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
