// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public static class UiLayoutFile
{
    public static string Write(UiLayoutDocument document)
    {
        var builder = new StringBuilder();
        builder.Append("version: 1\n");
        builder.Append("title: ").Append(Quote(document.Title)).Append('\n');
        builder.Append("columns: ").Append(document.Columns.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("rows: ").Append(document.Rows.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("code: ").Append(Quote(document.Code)).Append('\n');
        builder.Append("nextId: ").Append(document.NextId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("active: ").Append(document.ActiveTab.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("previewWidth: ").Append(document.PreviewWidth.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("previewHeight: ").Append(document.PreviewHeight.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("fancy: ").Append(document.Fancy ? "true" : "false").Append('\n');
        builder.Append("resizable: ").Append(document.Resizable ? "true" : "false").Append('\n');
        builder.Append("titleLoc: ").Append(Quote(document.TitleLoc)).Append('\n');
        builder.Append("tabs:\n");
        foreach (var tab in document.Tabs)
        {
            builder.Append("  - name: ").Append(Quote(tab.Name)).Append('\n');
            builder.Append("    elements:\n");
            foreach (var element in tab.Elements)
                WriteElement(builder, element);
        }

        return builder.ToString();
    }

    public static UiLayoutDocument Read(string text)
    {
        var document = new UiLayoutDocument();
        UiLayoutElement? current = null;
        UiLayoutTab? tab = document.Tabs[0];
        var inElements = false;
        var sawTab = false;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line == "tabs:")
            {
                inElements = false;
                current = null;
                continue;
            }

            if (line.StartsWith("- name:", StringComparison.Ordinal))
            {
                if (!sawTab)
                {
                    document.Tabs.Clear();
                    sawTab = true;
                }

                tab = new UiLayoutTab();
                document.Tabs.Add(tab);
                var value = line["- name:".Length..].Trim();
                tab.Name = Unquote(value);
                inElements = false;
                current = null;
                continue;
            }

            if (line == "elements:")
            {
                tab ??= document.Current;
                inElements = true;
                current = null;
                continue;
            }

            if (!inElements)
            {
                SetDocument(document, line);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                current = new UiLayoutElement();
                (tab ?? document.Current).Elements.Add(current);
                SetElement(current, line[2..]);
                continue;
            }

            if (current != null)
                SetElement(current, line);
        }

        if (document.Tabs.Count == 0)
            document.Tabs.Add(new UiLayoutTab());

        var maxId = 0;
        foreach (var page in document.Tabs)
        {
            foreach (var element in page.Elements)
                maxId = Math.Max(maxId, element.Id);
        }

        if (document.NextId <= maxId)
            document.NextId = maxId + 1;

        document.ActiveTab = Math.Clamp(document.ActiveTab, 0, document.Tabs.Count - 1);
        UiLayoutMath.ClampGrid(document);
        return document;
    }

    public static string Hex(Color color)
    {
        return $"#{color.RByte:X2}{color.GByte:X2}{color.BByte:X2}{color.AByte:X2}";
    }

    private static void WriteElement(StringBuilder builder, UiLayoutElement element)
    {
        builder.Append("  - id: ").Append(element.Id.ToString(CultureInfo.InvariantCulture)).Append('\n');
        Field(builder, "kind", element.Kind.ToString());
        Field(builder, "name", Quote(element.Name));
        Field(builder, "column", element.Column);
        Field(builder, "row", element.Row);
        Field(builder, "columnSpan", element.ColumnSpan);
        Field(builder, "rowSpan", element.RowSpan);
        Field(builder, "height", element.Height.ToString());
        Field(builder, "vertical", element.Vertical);
        Field(builder, "minWidth", element.MinWidth);
        Field(builder, "minHeight", element.MinHeight);
        Field(builder, "maxWidth", element.MaxWidth);
        Field(builder, "maxHeight", element.MaxHeight);
        Field(builder, "margin", element.Margin);
        Field(builder, "marginLeft", element.MarginLeft);
        Field(builder, "marginTop", element.MarginTop);
        Field(builder, "marginRight", element.MarginRight);
        Field(builder, "marginBottom", element.MarginBottom);
        Field(builder, "horizontalExpand", element.HorizontalExpand);
        Field(builder, "verticalExpand", element.VerticalExpand);
        Field(builder, "setWidth", element.SetWidth);
        Field(builder, "setHeight", element.SetHeight);
        Field(builder, "columns", element.Columns);
        Field(builder, "scaleX", element.ScaleX);
        Field(builder, "scaleY", element.ScaleY);
        Field(builder, "direction", Quote(element.Direction));
        Field(builder, "locId", Quote(element.LocId));
        Field(builder, "visible", element.Visible);
        Field(builder, "modulate", Hex(element.Modulate));
        Field(builder, "hAlign", element.HorizontalAlignment.ToString());
        Field(builder, "vAlign", element.VerticalAlignment.ToString());
        Field(builder, "text", Quote(element.Text));
        Field(builder, "placeHolder", Quote(element.PlaceHolder));
        Field(builder, "fontId", Quote(element.FontId));
        Field(builder, "fontSize", element.FontSize);
        Field(builder, "fontColor", Hex(element.FontColor));
        Field(builder, "fontColorSet", element.FontColorSet);
        Field(builder, "textAlign", element.TextAlign.ToString());
        Field(builder, "clipText", element.ClipText);
        Field(builder, "disabled", element.Disabled);
        Field(builder, "toggleMode", element.ToggleMode);
        Field(builder, "pressed", element.Pressed);
        Field(builder, "editable", element.Editable);
        Field(builder, "minValue", element.MinValue);
        Field(builder, "maxValue", element.MaxValue);
        Field(builder, "value", element.Value);
        Field(builder, "background", Hex(element.Background));
        Field(builder, "borderColor", Hex(element.BorderColor));
        Field(builder, "borderThickness", element.BorderThickness);
        Field(builder, "foreground", Hex(element.Foreground));
        Field(builder, "items", Quote(element.Items));
        Field(builder, "selected", element.Selected);
        Field(builder, "texturePath", Quote(element.TexturePath));
        Field(builder, "spriteState", Quote(element.SpriteState));
        Field(builder, "spriteDirection", element.SpriteDirection);
        Field(builder, "spriteFrame", element.SpriteFrame);
        Field(builder, "stretch", element.Stretch.ToString());
        Field(builder, "action", Quote(element.Action));
        Field(builder, "changedAction", Quote(element.ChangedAction));
        Field(builder, "actionNote", Quote(element.ActionNote));
        Field(builder, "primaryEvent", Quote(element.PrimaryEvent));
        Field(builder, "raisesPressed", element.RaisesPressed);
        Field(builder, "parentId", element.ParentId);
        Field(builder, "order", element.Order);
        Field(builder, "styleClasses", Quote(element.StyleClasses));
    }

    private static void Field(StringBuilder builder, string key, string value)
    {
        builder.Append("    ").Append(key).Append(": ").Append(value).Append('\n');
    }

    private static void Field(StringBuilder builder, string key, int value)
    {
        Field(builder, key, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Field(StringBuilder builder, string key, float value)
    {
        Field(builder, key, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Field(StringBuilder builder, string key, bool value)
    {
        Field(builder, key, value ? "true" : "false");
    }

    private static readonly Dictionary<string, Action<UiLayoutDocument, string>> DocumentFields = new()
    {
        ["title"] = (document, value) => document.Title = Unquote(value),
        ["columns"] = (document, value) => document.Columns = Int(value),
        ["rows"] = (document, value) => document.Rows = Int(value),
        ["code"] = (document, value) => document.Code = Unquote(value),
        ["nextId"] = (document, value) => document.NextId = Int(value),
        ["active"] = (document, value) => document.ActiveTab = Int(value),
        ["previewWidth"] = (document, value) => document.PreviewWidth = Float(value),
        ["previewHeight"] = (document, value) => document.PreviewHeight = Float(value),
        ["fancy"] = (document, value) => document.Fancy = Bool(value),
        ["resizable"] = (document, value) => document.Resizable = Bool(value),
        ["titleLoc"] = (document, value) => document.TitleLoc = Unquote(value),
    };

    private static readonly Dictionary<string, Action<UiLayoutElement, string>> ElementFields = new()
    {
        ["id"] = (element, value) => element.Id = Int(value),
        ["kind"] = (element, value) => SetEnum<UiElementKind>(value, kind => element.Kind = kind),
        ["name"] = (element, value) => element.Name = value,
        ["column"] = (element, value) => element.Column = Int(value),
        ["row"] = (element, value) => element.Row = Int(value),
        ["columnSpan"] = (element, value) => element.ColumnSpan = Int(value),
        ["rowSpan"] = (element, value) => element.RowSpan = Int(value),
        ["height"] = (element, value) => SetEnum<UiHeightMode>(value, height => element.Height = height),
        ["vertical"] = (element, value) => element.Vertical = Bool(value),
        ["minWidth"] = (element, value) => element.MinWidth = Float(value),
        ["minHeight"] = (element, value) => element.MinHeight = Float(value),
        ["maxWidth"] = (element, value) => element.MaxWidth = Float(value),
        ["maxHeight"] = (element, value) => element.MaxHeight = Float(value),
        ["margin"] = (element, value) => element.Margin = Float(value),
        ["marginLeft"] = (element, value) => element.MarginLeft = Float(value),
        ["marginTop"] = (element, value) => element.MarginTop = Float(value),
        ["marginRight"] = (element, value) => element.MarginRight = Float(value),
        ["marginBottom"] = (element, value) => element.MarginBottom = Float(value),
        ["horizontalExpand"] = (element, value) => element.HorizontalExpand = Bool(value),
        ["verticalExpand"] = (element, value) => element.VerticalExpand = Bool(value),
        ["setWidth"] = (element, value) => element.SetWidth = Float(value),
        ["setHeight"] = (element, value) => element.SetHeight = Float(value),
        ["columns"] = (element, value) => element.Columns = Math.Max(1, Int(value)),
        ["scaleX"] = (element, value) => element.ScaleX = Float(value),
        ["scaleY"] = (element, value) => element.ScaleY = Float(value),
        ["direction"] = (element, value) => element.Direction = value,
        ["locId"] = (element, value) => element.LocId = value,
        ["visible"] = (element, value) => element.Visible = Bool(value),
        ["modulate"] = (element, value) => element.Modulate = ParseColor(value),
        ["hAlign"] = (element, value) => SetEnum<Control.HAlignment>(value, align => element.HorizontalAlignment = align),
        ["vAlign"] = (element, value) => SetEnum<Control.VAlignment>(value, align => element.VerticalAlignment = align),
        ["text"] = (element, value) => element.Text = value,
        ["placeHolder"] = (element, value) => element.PlaceHolder = value,
        ["fontId"] = (element, value) => element.FontId = value,
        ["fontSize"] = (element, value) => element.FontSize = Int(value),
        ["fontColor"] = (element, value) => element.FontColor = ParseColor(value),
        ["fontColorSet"] = (element, value) => element.FontColorSet = Bool(value),
        ["textAlign"] = (element, value) => SetEnum<Label.AlignMode>(value, align => element.TextAlign = align),
        ["clipText"] = (element, value) => element.ClipText = Bool(value),
        ["disabled"] = (element, value) => element.Disabled = Bool(value),
        ["toggleMode"] = (element, value) => element.ToggleMode = Bool(value),
        ["pressed"] = (element, value) => element.Pressed = Bool(value),
        ["editable"] = (element, value) => element.Editable = Bool(value),
        ["minValue"] = (element, value) => element.MinValue = Float(value),
        ["maxValue"] = (element, value) => element.MaxValue = Float(value),
        ["value"] = (element, value) => element.Value = Float(value),
        ["background"] = (element, value) => element.Background = ParseColor(value),
        ["borderColor"] = (element, value) => element.BorderColor = ParseColor(value),
        ["borderThickness"] = (element, value) => element.BorderThickness = Float(value),
        ["foreground"] = (element, value) => element.Foreground = ParseColor(value),
        ["items"] = (element, value) => element.Items = value,
        ["selected"] = (element, value) => element.Selected = Int(value),
        ["texturePath"] = (element, value) => element.TexturePath = value,
        ["spriteState"] = (element, value) => element.SpriteState = value,
        ["spriteDirection"] = (element, value) => element.SpriteDirection = Int(value),
        ["spriteFrame"] = (element, value) => element.SpriteFrame = Int(value),
        ["stretch"] = (element, value) => SetEnum<TextureRect.StretchMode>(value, stretch => element.Stretch = stretch),
        ["action"] = (element, value) => element.Action = value,
        ["changedAction"] = (element, value) => element.ChangedAction = value,
        ["actionNote"] = (element, value) => element.ActionNote = value,
        ["primaryEvent"] = (element, value) => element.PrimaryEvent = value,
        ["raisesPressed"] = (element, value) => element.RaisesPressed = Bool(value),
        ["parentId"] = (element, value) => element.ParentId = Int(value),
        ["order"] = (element, value) => element.Order = Int(value),
        ["styleClasses"] = (element, value) => element.StyleClasses = value,
    };

    private static void SetDocument(UiLayoutDocument document, string line)
    {
        if (!Split(line, out var key, out var value))
            return;

        if (DocumentFields.TryGetValue(key, out var set))
            set(document, value);
    }

    private static void SetElement(UiLayoutElement element, string line)
    {
        if (!Split(line, out var key, out var raw))
            return;

        if (ElementFields.TryGetValue(key, out var set))
            set(element, Unquote(raw));
    }

    private static void SetEnum<T>(string value, Action<T> apply)
        where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, out var parsed))
            apply(parsed);
    }

    private static bool Split(string line, out string key, out string value)
    {
        var colon = line.IndexOf(':');
        if (colon <= 0)
        {
            key = "";
            value = "";
            return false;
        }

        key = line[..colon].Trim();
        value = line[(colon + 1)..].Trim();
        return key.Length > 0;
    }

    public static string Quote(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"')
            return value;

        var body = value[1..];
        if (body.EndsWith('"'))
            body = body[..^1];

        return body
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static int Int(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static float Float(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static bool Bool(string value)
    {
        return value == "true";
    }

    private static Color ParseColor(string value)
    {
        return Color.TryFromHex(value) ?? Color.White;
    }
}
