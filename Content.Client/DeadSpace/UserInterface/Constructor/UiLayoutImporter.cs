// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public static class UiLayoutImporter
{
    public static void Import(
        Control root,
        UiLayoutDocument document,
        string tabName,
        Action relayout,
        IResourceManager resources,
        ILocalizationManager loc)
    {
        var size = root.Size;
        if (size.X < 20 || size.Y < 20)
            size = root.DesiredSize;
        if (size.X < 20 || size.Y < 20)
            return;

        UiLayoutTab tab;
        if (document.Tabs.Count == 1 && document.Tabs[0].Elements.Count == 0)
            tab = document.Tabs[0];
        else
        {
            tab = new UiLayoutTab();
            document.Tabs.Add(tab);
        }

        tab.Name = tabName.Length == 0 ? "Окно" : tabName;
        tab.Elements.Clear();
        document.ActiveTab = document.Tabs.IndexOf(tab);

        foreach (var child in root.Children.ToArray())
            Walk(child, root, document, tab.Elements, 0, resources, loc);

        HarvestTabs(root, root, document, tabName, relayout, resources, loc);
    }

    private static void HarvestTabs(
        Control control,
        Control root,
        UiLayoutDocument document,
        string windowName,
        Action relayout,
        IResourceManager resources,
        ILocalizationManager loc)
    {
        if (control is TabContainer tabs)
        {
            for (var i = 0; i < tabs.ChildCount; i++)
            {
                tabs.CurrentTab = i;
                relayout();
                var page = tabs.GetChild(i);
                var title = tabs.GetActualTabTitle(i);
                var pageTab = new UiLayoutTab { Name = windowName + " / " + title };
                document.Tabs.Add(pageTab);
                foreach (var child in page.Children.ToArray())
                    Walk(child, root, document, pageTab.Elements, 0, resources, loc);
            }

            return;
        }

        foreach (var child in control.Children.ToArray())
            HarvestTabs(child, root, document, windowName, relayout, resources, loc);
    }

    private static void Walk(
        Control control,
        Control root,
        UiLayoutDocument document,
        List<UiLayoutElement> target,
        int parentId,
        IResourceManager resources,
        ILocalizationManager loc)
    {
        if (control.Parent is TabContainer)
            return;

        if (control is Label && control.Parent is Button or CheckBox)
            return;

        if (TryKind(control, out var kind))
        {
            var element = Take(control, root, document, kind, resources, loc);
            if (element != null)
            {
                element.ParentId = parentId;
                element.StyleClasses = string.Join(' ', control.StyleClasses);
                element.HorizontalExpand = control.HorizontalExpand;
                element.VerticalExpand = control.VerticalExpand;
                element.MarginLeft = control.Margin.Left;
                element.MarginTop = control.Margin.Top;
                element.MarginRight = control.Margin.Right;
                element.MarginBottom = control.Margin.Bottom;
                element.SetWidth = control.SetWidth;
                element.SetHeight = control.SetHeight;
                if (control is BoxContainer box)
                    element.Vertical = box.Orientation == BoxContainer.LayoutOrientation.Vertical;
                if (control is GridContainer grid)
                    element.Columns = Math.Max(1, grid.Columns);
                if (control is SpriteView view)
                {
                    element.ScaleX = view.Scale.X;
                    element.ScaleY = view.Scale.Y;
                    if (view.OverrideDirection is { } direction)
                        element.Direction = direction.ToString();
                }

                target.Add(element);
            }

            if (element != null && kind is UiElementKind.Box or UiElementKind.Grid or UiElementKind.Panel or UiElementKind.ProgressBar)
            {
                foreach (var child in control.Children.ToArray())
                    Walk(child, root, document, target, element.Id, resources, loc);
                return;
            }

            if (kind != UiElementKind.Panel)
                return;
        }

        foreach (var child in control.Children.ToArray())
            Walk(child, root, document, target, parentId, resources, loc);
    }

    private static UiLayoutElement? Take(
        Control control,
        Control root,
        UiLayoutDocument document,
        UiElementKind kind,
        IResourceManager resources,
        ILocalizationManager loc)
    {
        if (control.Size.X < 4 && control.Size.Y < 4 && string.IsNullOrWhiteSpace(control.Name) && control is not TextureButton)
            return null;

        var width = Math.Max(1f, root.Size.X);
        var height = Math.Max(1f, root.Size.Y);
        if (kind == UiElementKind.Panel &&
            control.Size.X > width * 0.88f &&
            control.Size.Y > height * 0.88f)
            return null;
        var local = control.GlobalPosition - root.GlobalPosition;
        var column = (int)Math.Floor(local.X / width * document.Columns);
        var row = (int)Math.Floor(local.Y / height * document.Rows);
        var columnSpan = Math.Max(1, (int)Math.Round(control.Size.X / width * document.Columns));
        var rowSpan = Math.Max(1, (int)Math.Round(control.Size.Y / height * document.Rows));
        column = Math.Clamp(column, 0, document.Columns - 1);
        row = Math.Clamp(row, 0, document.Rows - 1);
        columnSpan = Math.Min(columnSpan, document.Columns - column);
        rowSpan = Math.Min(rowSpan, document.Rows - row);

        var element = UiLayoutMath.Create(document, kind, column, row);
        element.Column = column;
        element.Row = row;
        element.ColumnSpan = columnSpan;
        element.RowSpan = rowSpan;
        element.MinWidth = Math.Max(control.Size.X, control is TextureButton ? 14 : 0);
        element.MinHeight = Math.Max(control.Size.Y, control is TextureButton ? 14 : 0);
        element.HorizontalAlignment = Control.HAlignment.Left;
        element.VerticalAlignment = Control.VAlignment.Top;
        element.Height = UiHeightMode.Content;
        element.Modulate = control.ActualModulateSelf;
        element.Text = "";
        element.Action = "";
        element.ChangedAction = "";
        element.ActionNote = "";
        element.PrimaryEvent = "";
        element.ForegroundSet = false;
        if (!string.IsNullOrWhiteSpace(control.Name))
            element.Name = control.Name;

        switch (control)
        {
            case SwitchButton toggle:
                element.Text = toggle.Pressed
                    ? toggle.OnStateLabel.Text ?? ""
                    : toggle.OffStateLabel.Text ?? "";
                element.Pressed = toggle.Pressed;
                element.LiveTexture = StyleTexture(toggle);
                element.PrimaryEvent = "OnPressed";
                break;
            case TextureButton textureButton:
                element.Text = "";
                element.RaisesPressed = true;
                element.LiveTexture = textureButton.TextureNormal ?? StyleTexture(textureButton);
                if (HasClass(textureButton, "windowCloseButton"))
                {
                    element.TexturePath = "/Textures/Interface/Nano/cross.svg.png";
                    element.Action = "CloseButtonPressed";
                    element.ActionNote = "Закрывает окно";
                }

                break;
            case CheckBox check:
                element.Text = check.Text ?? "";
                element.LocId = UiLocCatalog.FindId(resources, loc, element.Text) ?? "";
                element.Pressed = check.Pressed;
                break;
            case Button button:
                element.Text = button.Text ?? "";
                element.LocId = UiLocCatalog.FindId(resources, loc, element.Text) ?? "";
                break;
            case LineEdit edit:
                element.Text = edit.Text ?? "";
                element.PlaceHolder = edit.PlaceHolder ?? "";
                break;
            case Label label:
                element.Text = label.Text ?? "";
                element.LocId = UiLocCatalog.FindId(resources, loc, element.Text) ?? "";
                if (label.FontColorOverride is { } fontColor)
                {
                    element.FontColor = fontColor;
                    element.FontColorSet = true;
                }

                break;
            case PanelContainer panel when panel.PanelOverride is StyleBoxFlat flat:
                element.Background = flat.BackgroundColor;
                element.BorderColor = flat.BorderColor;
                element.BorderThickness = flat.BorderThickness.Left;
                break;
            case TextureRect texture:
                element.Stretch = texture.Stretch;
                element.LiveTexture = StyleTexture(texture);
                break;
            case SpriteView:
                element.Text = "";
                break;
            case Slider slider:
                element.MinValue = slider.MinValue;
                element.MaxValue = slider.MaxValue;
                element.Value = slider.Value;
                break;
            case ProgressBar bar:
                element.MinValue = bar.MinValue;
                element.MaxValue = bar.MaxValue;
                element.Value = bar.Value;
                element.ForegroundSet = bar.ForegroundStyleBoxOverride is StyleBoxFlat;
                if (bar.ForegroundStyleBoxOverride is StyleBoxFlat fill)
                    element.Foreground = fill.BackgroundColor;
                break;
            case SpinBox spin:
                element.Value = spin.Value;
                break;
        }

        if (kind == UiElementKind.Label && string.IsNullOrWhiteSpace(element.Text))
        {
            document.NextId = element.Id;
            return null;
        }

        if (kind == UiElementKind.Button &&
            string.IsNullOrWhiteSpace(element.Text) &&
            control.Size.X < 36 &&
            control.Size.Y < 36)
        {
            document.NextId = element.Id;
            return null;
        }

        if (element.Action.Length == 0 && UiElementKinds.Get(kind).PressedEvent != null)
            element.Action = FindHandler(root.GetType(), control.Name ?? "");
        if (element.Action is "Close" or "CloseButtonPressed")
            element.ActionNote = "Закрывает окно";

        return element;
    }

    private static string FindHandler(Type type, string controlName)
    {
        if (string.IsNullOrWhiteSpace(controlName))
            return "";

        var stem = controlName.EndsWith("Button") ? controlName[..^6] : controlName;
        var onStem = "On" + stem;
        var onName = "On" + controlName;
        var onNamePressed = onName + "Pressed";
        var namePressed = controlName + "Pressed";
        string? same = null;
        foreach (var method in type.GetMethods())
        {
            var name = method.Name;
            if (name.StartsWith("add_"))
                name = name[4..];
            else if (name.StartsWith("remove_") || name.StartsWith("get_") || name.StartsWith("set_"))
                continue;

            if (name == onStem || name == onName || name == onNamePressed || name == namePressed)
                return name;

            if (stem.Length > 0 && name == stem)
                same = name;
        }

        return same ?? "";
    }

    private static Texture? StyleTexture(Control control)
    {
        if (control.TryGetStyleProperty<Texture>("texture", out var texture))
            return texture;

        return null;
    }

    private static bool HasClass(Control control, string name)
    {
        foreach (var styleClass in control.StyleClasses)
        {
            if (styleClass == name)
                return true;
        }

        return false;
    }

    private static readonly (Type Type, UiElementKind Kind)[] ImportedKinds =
    {
        (typeof(SwitchButton), UiElementKind.CheckBox),
        (typeof(TextureButton), UiElementKind.TextureRect),
        (typeof(SpriteView), UiElementKind.SpriteView),
        (typeof(CheckBox), UiElementKind.CheckBox),
        (typeof(Button), UiElementKind.Button),
        (typeof(LineEdit), UiElementKind.LineEdit),
        (typeof(Slider), UiElementKind.Slider),
        (typeof(SpinBox), UiElementKind.SpinBox),
        (typeof(OptionButton), UiElementKind.OptionButton),
        (typeof(ItemList), UiElementKind.ItemList),
        (typeof(ProgressBar), UiElementKind.ProgressBar),
        (typeof(GridContainer), UiElementKind.Grid),
        (typeof(BoxContainer), UiElementKind.Box),
        (typeof(PanelContainer), UiElementKind.Panel),
        (typeof(TextureRect), UiElementKind.TextureRect),
        (typeof(RichTextLabel), UiElementKind.Label),
        (typeof(Label), UiElementKind.Label),
    };

    private static bool TryKind(Control control, out UiElementKind kind)
    {
        foreach (var (type, mapped) in ImportedKinds)
        {
            if (!type.IsInstanceOfType(control))
                continue;

            kind = mapped;
            return true;
        }

        kind = default;
        return false;
    }
}
