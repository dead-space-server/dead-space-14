// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public enum UiElementKind
{
    Label,
    Button,
    CheckBox,
    LineEdit,
    Slider,
    SpinBox,
    OptionButton,
    ItemList,
    ProgressBar,
    Panel,
    Separator,
    TextureRect,
    Box,
    Grid,
    SpriteView,
}

public enum UiHeightMode
{
    Content,
    Fill,
}

public sealed class UiLayoutElement
{
    public int Id;
    public UiElementKind Kind;
    public string Name = "";
    public int Column;
    public int Row;
    public int ColumnSpan = 4;
    public int RowSpan = 1;
    public UiHeightMode Height = UiHeightMode.Content;
    public float MinWidth;
    public float MinHeight;
    public float MaxWidth;
    public float MaxHeight;
    public float Margin;
    public float MarginLeft;
    public float MarginTop;
    public float MarginRight;
    public float MarginBottom;
    public bool HorizontalExpand;
    public bool VerticalExpand;
    public float SetWidth;
    public float SetHeight;
    public int Columns = 2;
    public float ScaleX = 1;
    public float ScaleY = 1;
    public string Direction = "";
    public string LocId = "";
    public bool Visible = true;
    public Color Modulate = Color.White;
    public Control.HAlignment HorizontalAlignment = Control.HAlignment.Stretch;
    public Control.VAlignment VerticalAlignment = Control.VAlignment.Top;

    public string Text = "";
    public string PlaceHolder = "";
    public string FontId = "";
    public int FontSize = 12;
    public Color FontColor = Color.White;
    public bool FontColorSet;
    public Label.AlignMode TextAlign = Label.AlignMode.Left;
    public bool ClipText;

    public bool Disabled;
    public bool ToggleMode;
    public bool Pressed;
    public bool Editable = true;

    public float MinValue;
    public float MaxValue = 100;
    public float Value;

    public Color Background = new(0.12f, 0.14f, 0.18f, 1f);
    public Color BorderColor = new(0.45f, 0.52f, 0.62f, 1f);
    public float BorderThickness = 1;
    public Color Foreground = new(0.25f, 0.62f, 0.95f, 1f);

    public string Items = "";
    public int Selected;
    public string TexturePath = "";
    public string SpriteState = "";
    public int SpriteDirection;
    public int SpriteFrame;
    public TextureRect.StretchMode Stretch = TextureRect.StretchMode.KeepAspectCentered;

    public string Action = "";
    public string ChangedAction = "";
    public string ActionNote = "";
    public string PrimaryEvent = "";
    public int ParentId;
    public int Order;
    public string StyleClasses = "";
    public bool Vertical;
    public bool ForegroundSet;
    public bool RaisesPressed;
    public Texture? LiveTexture;

    public UiLayoutElement Clone()
    {
        return (UiLayoutElement)MemberwiseClone();
    }
}

public sealed class UiLayoutTab
{
    public string Name = "Вкладка 1";
    public List<UiLayoutElement> Elements = new();
}

public sealed class UiLayoutDocument
{
    public string Title = "Окно";
    public int Columns = 12;
    public int Rows = 8;
    public string Code = "";
    public bool Fancy;
    public bool Resizable = true;
    public string TitleLoc = "";
    public int NextId = 1;
    public int ActiveTab;
    public float PreviewWidth;
    public float PreviewHeight;
    public List<UiLayoutTab> Tabs = new();

    public UiLayoutDocument()
    {
        Tabs.Add(new UiLayoutTab());
    }

    public UiLayoutTab Current => Tabs[Math.Clamp(ActiveTab, 0, Tabs.Count - 1)];

    public List<UiLayoutElement> Elements => Current.Elements;
}

public static class UiLayoutMath
{
    public static void ClampGrid(UiLayoutDocument document)
    {
        document.Columns = Math.Clamp(document.Columns, 1, 24);
        document.Rows = Math.Clamp(document.Rows, 1, 24);
        foreach (var tab in document.Tabs)
        {
            foreach (var element in tab.Elements)
                ClampElement(document, element);
        }
    }

    public static void ClampElement(UiLayoutDocument document, UiLayoutElement element)
    {
        element.ColumnSpan = Math.Clamp(element.ColumnSpan, 1, document.Columns);
        element.RowSpan = Math.Clamp(element.RowSpan, 1, document.Rows);
        element.Column = Math.Clamp(element.Column, 0, document.Columns - element.ColumnSpan);
        element.Row = Math.Clamp(element.Row, 0, document.Rows - element.RowSpan);
    }

    public static bool Overlaps(
        UiLayoutDocument document,
        UiLayoutElement? self,
        int column,
        int row,
        int columnSpan,
        int rowSpan)
    {
        foreach (var other in document.Elements)
        {
            if (self != null && ReferenceEquals(other, self))
                continue;

            if (other.ParentId != 0 || self != null && self.ParentId != 0)
                continue;

            if (UiElementKinds.IsBackdrop(other.Kind) || self != null && UiElementKinds.IsBackdrop(self.Kind))
                continue;

            var separated = column + columnSpan <= other.Column
                || other.Column + other.ColumnSpan <= column
                || row + rowSpan <= other.Row
                || other.Row + other.RowSpan <= row;
            if (!separated)
                return true;
        }

        return false;
    }

    public static bool TryFindFree(
        UiLayoutDocument document,
        int columnSpan,
        int rowSpan,
        out int column,
        out int row)
    {
        columnSpan = Math.Clamp(columnSpan, 1, document.Columns);
        rowSpan = Math.Clamp(rowSpan, 1, document.Rows);
        for (var r = 0; r <= document.Rows - rowSpan; r++)
        {
            for (var c = 0; c <= document.Columns - columnSpan; c++)
            {
                if (Overlaps(document, null, c, r, columnSpan, rowSpan))
                    continue;

                column = c;
                row = r;
                return true;
            }
        }

        column = 0;
        row = 0;
        return false;
    }

    public static UiLayoutElement Create(UiLayoutDocument document, UiElementKind kind, int column, int row)
    {
        var id = document.NextId++;
        var element = new UiLayoutElement
        {
            Id = id,
            Kind = kind,
            Column = column,
            Row = row,
            Name = kind.ToString() + id,
        };
        var spec = UiElementKinds.Get(kind);
        element.ColumnSpan = spec.ColumnSpan;
        element.RowSpan = spec.RowSpan;
        element.Height = spec.Height;
        element.MinWidth = spec.MinWidth;
        element.MinHeight = spec.MinHeight;
        element.Text = spec.Text;
        element.PlaceHolder = spec.PlaceHolder;
        element.Items = spec.Items;
        element.Value = spec.Value;
        element.ForegroundSet = spec.ForegroundSet;
        if (spec.LineColor is { } line)
            element.Background = line;

        ClampElement(document, element);
        return element;
    }
}
