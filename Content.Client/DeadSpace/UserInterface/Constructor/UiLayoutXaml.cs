// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public static class UiLayoutXaml
{
    public static string Write(UiLayoutDocument document)
    {
        var className = TypeName(document.Code.Length > 0 ? document.Code : document.Title);
        var width = document.PreviewWidth > 40 ? document.PreviewWidth : 760;
        var height = document.PreviewHeight > 40 ? document.PreviewHeight : 520;
        var root = document.Fancy ? "controls:FancyWindow" : "DefaultWindow";
        var builder = new StringBuilder();
        builder.Append('<').Append(root).Append(" xmlns=\"https://spacestation14.io\"\n");
        builder.Append("               xmlns:controls=\"clr-namespace:Content.Client.UserInterface.Controls\"\n");
        builder.Append("               xmlns:gfx=\"clr-namespace:Robust.Client.Graphics;assembly=Robust.Client\"\n");
        builder.Append("               Name=\"").Append(className).Append("\"\n");
        if (document.TitleLoc.Length > 0)
            builder.Append("               Title=\"{Loc '").Append(Escape(document.TitleLoc)).Append("'}\"\n");
        else
            builder.Append("               Title=\"").Append(Escape(document.Title)).Append("\"\n");
        builder.Append("               MinSize=\"").Append(Num(width)).Append(' ').Append(Num(height)).Append('"');
        if (!document.Resizable)
            builder.Append("\n               Resizable=\"False\"");
        builder.Append(">\n");

        var several = document.Tabs.Count > 1;
        if (several)
            builder.Append("    <TabContainer VerticalExpand=\"True\" HorizontalExpand=\"True\">\n");

        foreach (var tab in document.Tabs)
            WriteTab(builder, document, tab, several);

        if (several)
            builder.Append("    </TabContainer>\n");

        builder.Append("</").Append(root).Append(">\n");
        return builder.ToString();
    }

    private static void WriteTab(StringBuilder builder, UiLayoutDocument document, UiLayoutTab tab, bool nested)
    {
        var pad = nested ? "        " : "    ";
        builder.Append(pad).Append("<LayoutContainer");
        if (nested)
            builder.Append(" TabContainer.TabTitle=\"").Append(Escape(tab.Name)).Append('"');

        builder.Append(" HorizontalExpand=\"True\" VerticalExpand=\"True\">\n");
        var used = new HashSet<string>();
        foreach (var element in tab.Elements.Where(element => element.ParentId == 0).OrderBy(element => UiElementKinds.IsBackdrop(element.Kind) ? 0 : 1).ThenBy(element => element.Id))
            WriteElement(builder, document, tab.Elements, element, used, pad + "    ");

        builder.Append(pad).Append("</LayoutContainer>\n");
    }

    private static void WriteElement(
        StringBuilder builder,
        UiLayoutDocument document,
        List<UiLayoutElement> elements,
        UiLayoutElement element,
        HashSet<string> used,
        string pad)
    {
        var tag = Tag(element);
        var name = XmlName(element.Name, element.Kind, element.Id, used);

        var inner = pad + "    ";
        builder.Append(pad).Append('<').Append(tag);
        builder.Append(" Name=\"").Append(name).Append('"');
        builder.Append(" Access=\"Public\"");
        if (element.StyleClasses.Length > 0)
            builder.Append(" StyleClasses=\"").Append(Escape(element.StyleClasses)).Append('"');
        AppendBody(builder, element);
        if (element.ParentId == 0)
        {
            var left = (float)element.Column / document.Columns;
            var right = (float)(element.Column + element.ColumnSpan) / document.Columns;
            var top = (float)element.Row / document.Rows;
            var bottom = (float)(element.Row + element.RowSpan) / document.Rows;
            builder.Append('\n').Append(inner).Append("LayoutContainer.AnchorLeft=\"").Append(Num(left)).Append('"');
            builder.Append('\n').Append(inner).Append("LayoutContainer.AnchorTop=\"").Append(Num(top)).Append('"');
            builder.Append('\n').Append(inner).Append("LayoutContainer.AnchorRight=\"").Append(Num(right)).Append('"');
            builder.Append('\n').Append(inner).Append("LayoutContainer.AnchorBottom=\"").Append(Num(bottom)).Append('"');
        }

        if (element.MinWidth > 0 || element.MinHeight > 0)
        {
            builder.Append('\n').Append(inner).Append("MinSize=\"")
                .Append(Num(element.MinWidth)).Append(' ').Append(Num(Math.Max(element.MinHeight, element.Kind == UiElementKind.Separator ? 4 : 0))).Append('"');
        }
        else if (element.Kind == UiElementKind.Separator)
        {
            builder.Append('\n').Append(inner).Append("MinSize=\"0 4\"");
        }

        if (element.MaxWidth > 0 || element.MaxHeight > 0)
        {
            builder.Append('\n').Append(inner).Append("MaxSize=\"")
                .Append(Num(element.MaxWidth)).Append(' ').Append(Num(element.MaxHeight)).Append('"');
        }

        if (element.Margin > 0 || element.MarginLeft > 0 || element.MarginTop > 0 || element.MarginRight > 0 || element.MarginBottom > 0)
            builder.Append('\n').Append(inner).Append("Margin=\"").Append(MarginText(element)).Append('"');
        if (element.HorizontalAlignment != Control.HAlignment.Stretch)
            builder.Append('\n').Append(inner).Append("HorizontalAlignment=\"").Append(element.HorizontalAlignment).Append('"');
        if (element.VerticalAlignment != Control.VAlignment.Top)
            builder.Append('\n').Append(inner).Append("VerticalAlignment=\"").Append(element.VerticalAlignment).Append('"');
        if (!element.Visible)
            builder.Append('\n').Append(inner).Append("Visible=\"False\"");
        if (element.HorizontalExpand)
            builder.Append('\n').Append(inner).Append("HorizontalExpand=\"True\"");
        if (element.VerticalExpand)
            builder.Append('\n').Append(inner).Append("VerticalExpand=\"True\"");
        if (element.SetWidth > 0 && element.SetHeight > 0)
        {
            builder.Append('\n').Append(inner).Append("SetSize=\"")
                .Append(Num(element.SetWidth)).Append(' ').Append(Num(element.SetHeight)).Append('"');
        }

        var children = elements.Where(item => item.ParentId == element.Id).OrderBy(item => item.Order).ThenBy(item => item.Id).ToArray();
        if (!HasBody(element) && children.Length == 0)
        {
            builder.Append(" />\n");
            return;
        }

        builder.Append(">\n");
        if (element.Kind is UiElementKind.Panel or UiElementKind.Separator)
        {
            builder.Append(inner).Append('<').Append(tag).Append(".PanelOverride>\n");
            builder.Append(inner).Append("    <gfx:StyleBoxFlat BackgroundColor=\"").Append(UiLayoutFile.Hex(element.Background)).Append('"');
            if (element.Kind == UiElementKind.Panel)
            {
                builder.Append(" BorderColor=\"").Append(UiLayoutFile.Hex(element.BorderColor)).Append('"');
                builder.Append(" BorderThickness=\"").Append(Num(element.BorderThickness)).Append('"');
            }

            builder.Append(" />\n");
            builder.Append(inner).Append("</").Append(tag).Append(".PanelOverride>\n");
        }

        if (element.Kind == UiElementKind.ProgressBar && element.ForegroundSet)
        {
            builder.Append(inner).Append("<ProgressBar.ForegroundStyleBoxOverride>\n");
            builder.Append(inner).Append("    <gfx:StyleBoxFlat BackgroundColor=\"").Append(UiLayoutFile.Hex(element.Foreground)).Append("\" />\n");
            builder.Append(inner).Append("</ProgressBar.ForegroundStyleBoxOverride>\n");
        }

        foreach (var child in children)
            WriteElement(builder, document, elements, child, used, inner);

        builder.Append(pad).Append("</").Append(tag).Append(">\n");
    }

    private static bool HasBody(UiLayoutElement element)
    {
        return element.Kind is UiElementKind.Panel or UiElementKind.Separator
            || element.Kind == UiElementKind.ProgressBar && element.ForegroundSet;
    }

    private static void AppendBody(StringBuilder builder, UiLayoutElement element)
    {
        switch (element.Kind)
        {
            case UiElementKind.Label:
                AppendText(builder, element);
                if (element.FontColorSet)
                    builder.Append(" FontColorOverride=\"").Append(UiLayoutFile.Hex(element.FontColor)).Append('"');
                if (element.TextAlign != Label.AlignMode.Left)
                    builder.Append(" Align=\"").Append(element.TextAlign).Append('"');
                if (element.ClipText)
                    builder.Append(" ClipText=\"True\"");
                break;
            case UiElementKind.Button:
                AppendText(builder, element);
                if (element.Disabled)
                    builder.Append(" Disabled=\"True\"");
                if (element.ToggleMode)
                    builder.Append(" ToggleMode=\"True\"");
                if (element.Pressed)
                    builder.Append(" Pressed=\"True\"");
                break;
            case UiElementKind.CheckBox:
                if (element.PrimaryEvent != "OnPressed")
                    AppendText(builder, element);
                if (element.Pressed)
                    builder.Append(" Pressed=\"True\"");
                if (element.Disabled)
                    builder.Append(" Disabled=\"True\"");
                break;
            case UiElementKind.LineEdit:
                if (element.Text.Length > 0)
                    builder.Append(" Text=\"").Append(Escape(element.Text)).Append('"');
                if (element.PlaceHolder.Length > 0)
                    builder.Append(" PlaceHolder=\"").Append(Escape(element.PlaceHolder)).Append('"');
                if (!element.Editable)
                    builder.Append(" Editable=\"False\"");
                break;
            case UiElementKind.Slider:
            case UiElementKind.ProgressBar:
                builder.Append(" MinValue=\"").Append(Num(element.MinValue)).Append('"');
                builder.Append(" MaxValue=\"").Append(Num(element.MaxValue)).Append('"');
                builder.Append(" Value=\"").Append(Num(element.Value)).Append('"');
                break;
            case UiElementKind.SpinBox:
                builder.Append(" Value=\"").Append(Num(element.Value)).Append('"');
                break;
            case UiElementKind.TextureRect:
                if (element.TexturePath.Length > 0)
                    builder.Append(" TexturePath=\"").Append(Escape(element.TexturePath)).Append('"');
                builder.Append(" Stretch=\"").Append(element.Stretch).Append('"');
                break;
            case UiElementKind.Box:
                builder.Append(" Orientation=\"").Append(element.Vertical ? "Vertical" : "Horizontal").Append('"');
                break;
            case UiElementKind.Grid:
                builder.Append(" Columns=\"").Append(element.Columns.ToString(CultureInfo.InvariantCulture)).Append('"');
                break;
            case UiElementKind.SpriteView:
                if (element.ScaleX != 1 || element.ScaleY != 1)
                    builder.Append(" Scale=\"").Append(Num(element.ScaleX)).Append(' ').Append(Num(element.ScaleY)).Append('"');
                if (element.Direction.Length > 0)
                    builder.Append(" OverrideDirection=\"").Append(Escape(element.Direction)).Append('"');
                break;
        }

        if (element.Modulate != Color.White)
            builder.Append(" Modulate=\"").Append(UiLayoutFile.Hex(element.Modulate)).Append('"');
    }

    private static void AppendText(StringBuilder builder, UiLayoutElement element)
    {
        if (element.LocId.Length > 0)
            builder.Append(" Text=\"{Loc '").Append(Escape(element.LocId)).Append("'}\"");
        else if (element.Text.Length > 0)
            builder.Append(" Text=\"").Append(Escape(element.Text)).Append('"');
    }

    private static string MarginText(UiLayoutElement element)
    {
        var left = element.MarginLeft;
        var top = element.MarginTop;
        var right = element.MarginRight;
        var bottom = element.MarginBottom;
        if (left == 0 && top == 0 && right == 0 && bottom == 0)
            return Num(element.Margin);

        if (left == top && top == right && right == bottom)
            return Num(left);

        return Num(left) + " " + Num(top) + " " + Num(right) + " " + Num(bottom);
    }

    private static string Tag(UiLayoutElement element)
    {
        if (element.Kind == UiElementKind.CheckBox && element.PrimaryEvent == "OnPressed")
            return "controls:SwitchButton";
        if (element.Kind == UiElementKind.TextureRect && element.RaisesPressed)
            return "TextureButton";

        return UiElementKinds.Get(element.Kind).XamlTag;
    }

    private static string XmlName(string name, UiElementKind kind, int id, HashSet<string> used)
    {
        var filtered = new string(name.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
        if (filtered.Length == 0 || !char.IsLetter(filtered[0]) && filtered[0] != '_')
            filtered = kind + filtered;
        if (filtered.Length == 0)
            filtered = "Element" + id.ToString(CultureInfo.InvariantCulture);

        var unique = filtered;
        var suffix = 2;
        while (!used.Add(unique))
        {
            unique = filtered + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return unique;
    }

    private static string TypeName(string name)
    {
        var filtered = new string(name.Where(character => char.IsLetterOrDigit(character)).ToArray());
        if (filtered.Length == 0 || !char.IsLetter(filtered[0]))
            return "ConstructorWindow";

        return filtered;
    }

    private static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string Num(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
