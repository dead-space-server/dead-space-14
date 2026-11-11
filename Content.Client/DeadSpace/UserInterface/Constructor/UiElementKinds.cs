// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiElementSpec
{
    public required UiElementKind Kind { get; init; }
    public required string Title { get; init; }
    public int ColumnSpan { get; init; } = 4;
    public int RowSpan { get; init; } = 1;
    public UiHeightMode Height { get; init; } = UiHeightMode.Content;
    public float MinWidth { get; init; }
    public float MinHeight { get; init; }
    public string Text { get; init; } = "";
    public string PlaceHolder { get; init; } = "";
    public string Items { get; init; } = "";
    public float Value { get; init; }
    public bool ForegroundSet { get; init; }
    public Color? LineColor { get; init; }
    public bool Backdrop { get; init; }
    public string XamlTag { get; init; } = "Control";
    public string? PressedEvent { get; init; }
    public string? ChangedEvent { get; init; }
    public required Func<Control> CreateControl { get; init; }
}

public static class UiElementKinds
{
    public static readonly UiElementSpec[] All =
    {
        Spec(UiElementKind.Label, "Подпись", text: "Подпись", tag: "Label"),
        Spec(UiElementKind.Button, "Кнопка", columnSpan: 3, text: "Кнопка", pressed: "OnPressed", tag: "Button", create: static () => new Button()),
        Spec(UiElementKind.CheckBox, "Галочка", text: "Галочка", pressed: "OnToggled", tag: "CheckBox", create: static () => new CheckBox()),
        Spec(UiElementKind.LineEdit, "Поле", placeHolder: "Текст", pressed: "OnTextEntered", changed: "OnTextChanged", tag: "LineEdit", create: static () => new LineEdit()),
        Spec(UiElementKind.Slider, "Слайдер", minHeight: 20, value: 50, pressed: "OnValueChanged", tag: "Slider", create: static () => new Slider()),
        Spec(UiElementKind.SpinBox, "Число", columnSpan: 3, value: 1, pressed: "OnValueChanged", tag: "SpinBox", create: static () =>
        {
            var spin = new SpinBox();
            spin.InitDefaultButtons();
            return spin;
        }),
        Spec(UiElementKind.OptionButton, "Выпадающий список", items: "Первый|Второй|Третий", pressed: "OnItemSelected", tag: "OptionButton", create: static () => new OptionButton()),
        Spec(UiElementKind.ItemList, "Список строк", rowSpan: 3, height: UiHeightMode.Fill, minHeight: 64, items: "Строка 1|Строка 2|Строка 3", pressed: "OnItemSelected", tag: "ItemList", create: static () => new ItemList()),
        Spec(UiElementKind.ProgressBar, "Прогресс", minHeight: 18, value: 40, foreground: true, tag: "ProgressBar", create: static () => new ProgressBar()),
        Spec(UiElementKind.Panel, "Панель", rowSpan: 2, height: UiHeightMode.Fill, backdrop: true, tag: "PanelContainer", create: static () => new PanelContainer()),
        Spec(UiElementKind.Separator, "Линия", columnSpan: 12, minHeight: 4, line: new Color(0.75f, 0.8f, 0.88f, 1f), tag: "PanelContainer", create: static () => new PanelContainer()),
        Spec(UiElementKind.TextureRect, "Картинка", columnSpan: 2, rowSpan: 2, height: UiHeightMode.Fill, minWidth: 32, minHeight: 32, pressed: "OnPressed", tag: "TextureRect", create: static () => new TextureRect()),
        Spec(UiElementKind.Box, "Блок", columnSpan: 6, rowSpan: 3, height: UiHeightMode.Fill, minHeight: 48, tag: "BoxContainer", create: static () => new BoxContainer()),
        Spec(UiElementKind.Grid, "Сетка", columnSpan: 6, rowSpan: 3, height: UiHeightMode.Fill, minHeight: 48, tag: "GridContainer", create: static () => new GridContainer()),
        Spec(UiElementKind.SpriteView, "Спрайт", columnSpan: 2, rowSpan: 2, height: UiHeightMode.Fill, minWidth: 64, minHeight: 64, tag: "SpriteView", create: static () => new SpriteView()),
    };

    private static readonly Dictionary<UiElementKind, UiElementSpec> ByKind = new();

    static UiElementKinds()
    {
        foreach (var spec in All)
            ByKind.Add(spec.Kind, spec);
    }

    public static UiElementSpec Get(UiElementKind kind)
    {
        return ByKind[kind];
    }

    public static bool IsBackdrop(UiElementKind kind)
    {
        return ByKind.TryGetValue(kind, out var spec) && spec.Backdrop;
    }

    private static UiElementSpec Spec(
        UiElementKind kind,
        string title,
        int columnSpan = 4,
        int rowSpan = 1,
        UiHeightMode height = UiHeightMode.Content,
        float minWidth = 0,
        float minHeight = 0,
        string text = "",
        string placeHolder = "",
        string items = "",
        float value = 0,
        bool foreground = false,
        Color? line = null,
        bool backdrop = false,
        string? pressed = null,
        string? changed = null,
        string? tag = null,
        Func<Control>? create = null)
    {
        return new UiElementSpec
        {
            Kind = kind,
            Title = title,
            ColumnSpan = columnSpan,
            RowSpan = rowSpan,
            Height = height,
            MinWidth = minWidth,
            MinHeight = minHeight,
            Text = text,
            PlaceHolder = placeHolder,
            Items = items,
            Value = value,
            ForegroundSet = foreground,
            LineColor = line,
            Backdrop = backdrop,
            PressedEvent = pressed,
            ChangedEvent = changed,
            XamlTag = tag ?? "Control",
            CreateControl = create ?? (static () => new Label()),
        };
    }
}
