// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Asynchronous;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.Processing;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiConstructorWindow : DefaultWindow
{
    private static readonly ResPath SaveDirectory = new("/UiConstructor");

    [Dependency] private readonly IClipboardManager _clipboard = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IDynamicTypeFactory _types = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly ITaskManager _tasks = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly UiLayoutFactory _factory;
    private readonly UiConstructorSurface _surface;
    private readonly BoxContainer _inspector = new()
    {
        Orientation = LayoutOrientation.Vertical,
        HorizontalExpand = true,
        Margin = new Thickness(6),
    };
    private readonly BoxContainer _events = new()
    {
        Orientation = LayoutOrientation.Vertical,
        HorizontalExpand = true,
        Margin = new Thickness(6),
        Visible = false,
    };
    private readonly BoxContainer _tabs = new()
    {
        Orientation = LayoutOrientation.Horizontal,
        SeparationOverride = 4,
    };
    private readonly LineEdit _tabName = new() { MinSize = new Vector2(180, 0), HorizontalExpand = true };
    private readonly Label _status = new() { HorizontalExpand = true, ClipText = true };
    private readonly LineEdit _title = new() { HorizontalExpand = true, MinSize = new Vector2(120, 0) };
    private readonly LineEdit _file = new() { HorizontalExpand = true, MinSize = new Vector2(100, 0) };
    private readonly LineEdit _code = new() { HorizontalExpand = true, MinSize = new Vector2(160, 0) };
    private readonly SpinBox _columns = new();
    private readonly SpinBox _rows = new();
    private UiLayoutDocument _document = new();
    private SpinBox? _column;
    private SpinBox? _row;
    private SpinBox? _columnSpan;
    private SpinBox? _rowSpan;
    private bool _suppress;
    private bool _showEvents;
    private EyedropperCatcher? _catcher;

    public UiConstructorWindow()
    {
        IoCManager.InjectDependencies(this);
        _factory = new UiLayoutFactory();
        _surface = new UiConstructorSurface(_document, _factory, true);

        Title = "Конструктор UI";
        MinSize = new Vector2(1100, 680);
        SetSize = new Vector2(1240, 800);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };
        root.AddChild(BuildPalette());
        root.AddChild(BuildCenter());
        root.AddChild(BuildInspector());
        Contents.AddChild(root);

        _surface.Changed += () =>
        {
            WriteGridSpins();
        };
        _surface.SelectionChanged += RebuildInspector;
        _surface.Status += SetStatus;

        _columns.InitDefaultButtons();
        _rows.InitDefaultButtons();
        FillHeader();
        _columns.ValueChanged += _ => CommitColumns();
        _rows.ValueChanged += _ => CommitRows();
        _title.OnTextChanged += _ =>
        {
            if (_suppress)
                return;

            _document.Title = _title.Text;
        };
        _code.OnTextChanged += _ =>
        {
            if (_suppress)
                return;

            _document.Code = _code.Text.Trim();
        };
        _tabName.OnTextEntered += _ => CommitTabName();
        _tabName.OnFocusExit += _ => CommitTabName();
        OnClose += CancelPick;

        RebuildInspector();
        RefreshTabs();
        SetStatus("");
    }

    private Control BuildPalette()
    {
        var scroll = new ScrollContainer
        {
            MinSize = new Vector2(196, 0),
            HorizontalExpand = false,
            VerticalExpand = true,
        };
        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Margin = new Thickness(4),
        };
        list.AddChild(new Label
        {
            Text = "Элементы",
            Margin = new Thickness(0, 0, 0, 4),
        });

        foreach (var spec in UiElementKinds.All)
        {
            var button = new PaletteButton
            {
                Kind = spec.Kind,
                Text = spec.Title,
                HorizontalExpand = true,
                MouseUi = MouseUi,
            };
            button.Pick += Pick;
            button.Drop += Drop;
            list.AddChild(button);
        }

        scroll.AddChild(list);
        return scroll;
    }

    private Control BuildCenter()
    {
        var column = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };

        var names = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        names.AddChild(new Label { Text = "Заголовок", VerticalAlignment = Control.VAlignment.Center });
        names.AddChild(_title);
        names.AddChild(MakeButton("Строка", PickTitleLoc));
        names.AddChild(new Label { Text = "Файл", VerticalAlignment = Control.VAlignment.Center });
        names.AddChild(_file);
        column.AddChild(names);

        var gridRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        gridRow.AddChild(new Label { Text = "Колонки", VerticalAlignment = Control.VAlignment.Center });
        gridRow.AddChild(_columns);
        gridRow.AddChild(new Label { Text = "Ряды", VerticalAlignment = Control.VAlignment.Center });
        gridRow.AddChild(_rows);
        gridRow.AddChild(new Label { Text = "Класс", VerticalAlignment = Control.VAlignment.Center });
        gridRow.AddChild(_code);
        column.AddChild(gridRow);

        var fileRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        fileRow.AddChild(MakeButton("Новое", NewDocument));
        fileRow.AddChild(MakeButton("Открыть", OpenDialog));
        fileRow.AddChild(MakeButton("Сохранить", Save));
        fileRow.AddChild(MakeButton("Предпросмотр", OpenPreview));
        fileRow.AddChild(MakeButton("Из игры", OpenGameWindows));
        fileRow.AddChild(MakeButton("XAML", CopyXaml));
        column.AddChild(fileRow);

        var editRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        editRow.AddChild(MakeButton("Удалить", DeleteSelected));
        editRow.AddChild(MakeButton("Копия", DuplicateSelected));
        editRow.AddChild(_status);
        column.AddChild(editRow);

        var tabHeader = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        tabHeader.AddChild(new Label { Text = "Вкладка", VerticalAlignment = Control.VAlignment.Center });
        tabHeader.AddChild(_tabName);
        tabHeader.AddChild(MakeButton("Новая", AddTab));
        tabHeader.AddChild(MakeButton("Убрать", CloseTab));
        column.AddChild(tabHeader);
        var tabScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = false,
            MinSize = new Vector2(0, 36),
            MouseFilter = MouseFilterMode.Stop,
        };
        tabScroll.AddChild(_tabs);
        column.AddChild(tabScroll);

        _surface.HorizontalExpand = true;
        _surface.VerticalExpand = true;
        column.AddChild(_surface);
        return column;
    }

    private Control BuildInspector()
    {
        var panel = SolidPanel(360);
        var column = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var pages = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(6, 6, 6, 0),
        };
        pages.AddChild(MakeButton("Свойства", () =>
        {
            _showEvents = false;
            ApplyInspectorPage();
        }));
        pages.AddChild(MakeButton("События", () =>
        {
            _showEvents = true;
            ApplyInspectorPage();
        }));
        column.AddChild(pages);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Stop,
        };
        var stack = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        stack.AddChild(_inspector);
        stack.AddChild(_events);
        scroll.AddChild(stack);
        column.AddChild(scroll);
        panel.AddChild(column);
        return panel;
    }

    private void ApplyInspectorPage()
    {
        _inspector.Visible = !_showEvents;
        _events.Visible = _showEvents;
    }

    private void Pick(UiElementKind kind)
    {
        if (_surface.Armed == kind)
        {
            _surface.Armed = null;
            return;
        }

        _surface.Armed = kind;
    }

    private void Drop(UiElementKind kind, Vector2 global)
    {
        if (_surface.TryDrop(kind, global))
            _surface.Armed = null;
    }

    private void FillHeader()
    {
        _suppress = true;
        _title.Text = _document.Title;
        _code.Text = _document.Code;
        if (_file.Text.Length == 0)
            _file.Text = "Окно";
        _columns.Value = _document.Columns;
        _rows.Value = _document.Rows;
        _suppress = false;
    }

    private void CommitColumns()
    {
        if (_suppress)
            return;

        _document.Columns = _columns.Value;
        _surface.Relayout();
    }

    private void CommitRows()
    {
        if (_suppress)
            return;

        _document.Rows = _rows.Value;
        _surface.Relayout();
    }

    private void NewDocument()
    {
        _document = new UiLayoutDocument();
        _file.Text = "Окно";
        FillHeader();
        _surface.SetDocument(_document);
        RefreshTabs();
    }

    private void Save()
    {
        var name = Sanitize(_file.Text);
        _file.Text = name;
        _resources.UserData.CreateDir(SaveDirectory);
        using (var writer = _resources.UserData.OpenWriteText(SaveDirectory / $"{name}.yml"))
            writer.Write(UiLayoutFile.Write(_document));
        using (var writer = _resources.UserData.OpenWriteText(SaveDirectory / $"{name}.xaml"))
            writer.Write(UiLayoutXaml.Write(_document));
        SetStatus($"Сохранено: UiConstructor/{name}.xaml");
    }

    private void OpenDialog()
    {
        var dialog = new DefaultWindow
        {
            Title = "Открыть",
            MinSize = new Vector2(280, 320),
            SetSize = new Vector2(320, 420),
        };
        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Margin = new Thickness(4),
        };
        if (_resources.UserData.IsDir(SaveDirectory))
        {
            foreach (var entry in _resources.UserData.DirectoryEntries(SaveDirectory).OrderBy(item => item))
            {
                if (!entry.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = entry;
                var button = new Button { Text = fileName, HorizontalExpand = true };
                button.OnPressed += _ =>
                {
                    Load(fileName);
                    dialog.Close();
                };
                list.AddChild(button);
            }
        }

        if (list.ChildCount == 0)
            list.AddChild(new Label { Text = "Сохранённых окон нет." });

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(list);
        dialog.Contents.AddChild(scroll);
        dialog.Open();
    }

    private void Load(string fileName)
    {
        if (!_resources.UserData.TryReadAllText(SaveDirectory / fileName, out var text))
        {
            SetStatus("Файл не прочитался.");
            return;
        }

        _document = UiLayoutFile.Read(text);
        _file.Text = StripExtension(fileName);
        FillHeader();
        _surface.SetDocument(_document);
        RefreshTabs();
        SetStatus($"Открыто: {fileName}");
    }

    private void OpenPreview()
    {
        var clone = UiLayoutFile.Read(UiLayoutFile.Write(_document));
        new UiLayoutPreviewWindow(clone, _factory).Open();
    }

    private void DeleteSelected()
    {
        if (_surface.Selected == null)
            return;

        _surface.DeleteSelected();
    }

    private void DuplicateSelected()
    {
        _surface.DuplicateSelected();
    }

    private void PickTitleLoc()
    {
        OpenLoc(id =>
        {
            _document.TitleLoc = id;
            if (_loc.TryGetString(id, out var value))
            {
                _document.Title = value;
                _title.Text = value;
            }
        });
    }

    private void OpenLoc(Action<string> apply)
    {
        var browser = new UiLocBrowserWindow();
        browser.OnSelected += choice => apply(choice.Id);
        browser.Open();
    }

    private void CopyXaml()
    {
        var text = UiLayoutXaml.Write(_document);
        _clipboard.SetText(text);
        var dialog = new DefaultWindow
        {
            Title = "XAML",
            MinSize = new Vector2(720, 480),
            SetSize = new Vector2(900, 640),
        };
        var edit = new TextEdit
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(8),
        };
        edit.TextRope = new Rope.Leaf(text);
        dialog.Contents.AddChild(edit);
        dialog.Open();
        SetStatus("XAML скопирован.");
    }

    private void RebuildInspector()
    {
        _inspector.RemoveAllChildren();
        _events.RemoveAllChildren();
        _column = null;
        _row = null;
        _columnSpan = null;
        _rowSpan = null;
        var element = _surface.Selected;
        if (element == null)
        {
            ApplyInspectorPage();
            return;
        }

        var common = Section("Элемент");
        AddLine(common, "Имя", element.Name, value => element.Name = value);
        AddLine(common, "Стили", element.StyleClasses, value => element.StyleClasses = value);
        AddLine(common, "Строка", element.LocId, value => element.LocId = value);
        common.AddChild(MakeButton("Выбрать строку", () => OpenLoc(picked =>
        {
            element.LocId = picked;
            if (_loc.TryGetString(picked, out var value))
                element.Text = value;
            _surface.SyncSelected();
            RebuildInspector();
        })));
        AddParent(common, element);
        if (element.Kind == UiElementKind.Box)
            AddCheck(common, "Вертикально", element.Vertical, value => element.Vertical = value);
        if (element.Kind == UiElementKind.Grid)
            AddInt(common, "Колонки", element.Columns, value => element.Columns = Math.Max(1, value));
        if (element.Kind is UiElementKind.Box or UiElementKind.Grid)
            AddChildren(element);

        if (element.ParentId == 0)
        {
            var grid = Section("Сетка");
            _column = AddGrid(grid, "Колонка", element.Column);
            _row = AddGrid(grid, "Ряд", element.Row);
            _columnSpan = AddGrid(grid, "Ширина", element.ColumnSpan);
            _rowSpan = AddGrid(grid, "Высота", element.RowSpan);
            AddChoice(grid, "По высоте", element.Height, new (UiHeightMode, string)[]
            {
                (UiHeightMode.Content, "По содержимому"),
                (UiHeightMode.Fill, "На всю клетку"),
            }, value => element.Height = value);
        }
        else
        {
            var order = Section("Порядок");
            order.AddChild(MakeButton("Раньше", () => Nudge(element, -1)));
            order.AddChild(MakeButton("Позже", () => Nudge(element, 1)));
        }

        var size = Section("Размер");
        AddFloat(size, "Мин. ширина", element.MinWidth, value => element.MinWidth = value);
        AddFloat(size, "Мин. высота", element.MinHeight, value => element.MinHeight = value);
        AddFloat(size, "Макс. ширина", element.MaxWidth, value => element.MaxWidth = value);
        AddFloat(size, "Макс. высота", element.MaxHeight, value => element.MaxHeight = value);
        AddFloat(size, "Отступ", element.Margin, value => element.Margin = value);
        AddChoice(size, "По горизонтали", element.HorizontalAlignment, new (Control.HAlignment, string)[]
        {
            (Control.HAlignment.Stretch, "На ширину клетки"),
            (Control.HAlignment.Left, "Слева"),
            (Control.HAlignment.Center, "По центру"),
            (Control.HAlignment.Right, "Справа"),
        }, value => element.HorizontalAlignment = value);
        AddChoice(size, "По вертикали", element.VerticalAlignment, new (Control.VAlignment, string)[]
        {
            (Control.VAlignment.Top, "Сверху"),
            (Control.VAlignment.Center, "По центру"),
            (Control.VAlignment.Bottom, "Снизу"),
            (Control.VAlignment.Stretch, "На высоту клетки"),
        }, value => element.VerticalAlignment = value);

        if (element.Kind is UiElementKind.Label or UiElementKind.Button or UiElementKind.CheckBox)
        {
            var text = Section("Текст");
            AddLine(text, "Текст", element.Text, value => element.Text = value);
            AddFont(text, element);
            AddInt(text, "Размер", element.FontSize, value => element.FontSize = Math.Clamp(value, 1, 128));
            AddColor(text, "Цвет текста", element.FontColor, true, element.FontColorSet, (color, custom) =>
            {
                element.FontColor = color;
                element.FontColorSet = custom;
            });
            AddChoice(text, "Выравнивание", element.TextAlign, new (Label.AlignMode, string)[]
            {
                (Label.AlignMode.Left, "Слева"),
                (Label.AlignMode.Center, "По центру"),
                (Label.AlignMode.Right, "Справа"),
                (Label.AlignMode.Fill, "По ширине"),
            }, value => element.TextAlign = value);
            AddCheck(text, "Обрезать текст", element.ClipText, value => element.ClipText = value);
        }

        if (element.Kind == UiElementKind.LineEdit)
        {
            var text = Section("Поле");
            AddLine(text, "Текст", element.Text, value => element.Text = value);
            AddLine(text, "Заполнитель", element.PlaceHolder, value => element.PlaceHolder = value);
            AddCheck(text, "Можно править", element.Editable, value => element.Editable = value);
        }

        if (element.Kind is UiElementKind.Button or UiElementKind.CheckBox or UiElementKind.Slider)
            AddCheck(Section("Состояние"), "Отключено", element.Disabled, value => element.Disabled = value);
        if (element.Kind == UiElementKind.Button)
        {
            AddCheck(_inspector, "Переключатель", element.ToggleMode, value => element.ToggleMode = value);
            AddCheck(_inspector, "Нажата", element.Pressed, value => element.Pressed = value);
        }

        if (element.Kind == UiElementKind.CheckBox)
            AddCheck(Section("Состояние"), "Включена", element.Pressed, value => element.Pressed = value);

        if (element.Kind is UiElementKind.Slider or UiElementKind.ProgressBar)
        {
            var range = Section("Значение");
            AddFloat(range, "Минимум", element.MinValue, value => element.MinValue = value);
            AddFloat(range, "Максимум", element.MaxValue, value => element.MaxValue = value);
            AddFloat(range, "Сейчас", element.Value, value => element.Value = value);
        }

        if (element.Kind == UiElementKind.SpinBox)
            AddFloat(Section("Значение"), "Сейчас", element.Value, value => element.Value = value);

        if (element.Kind is UiElementKind.OptionButton or UiElementKind.ItemList)
        {
            var items = Section("Пункты");
            AddLine(items, "Пункты", element.Items, value => element.Items = value);
            if (element.Kind == UiElementKind.OptionButton)
                AddInt(items, "Выбран", element.Selected, value => element.Selected = Math.Max(0, value));
        }

        if (element.Kind == UiElementKind.Panel)
        {
            var paint = Section("Панель");
            AddColor(paint, "Фон", element.Background, false, true, (color, _) => element.Background = color);
            AddColor(paint, "Рамка", element.BorderColor, false, true, (color, _) => element.BorderColor = color);
            AddFloat(paint, "Толщина", element.BorderThickness, value => element.BorderThickness = value);
        }

        if (element.Kind == UiElementKind.Separator)
        {
            var line = Section("Линия");
            AddColor(line, "Цвет", element.Background, false, true, (color, _) => element.Background = color);
            AddCheck(line, "Вертикально", element.Vertical, value =>
            {
                element.Vertical = value;
                if (value)
                {
                    element.ColumnSpan = 1;
                    element.RowSpan = Math.Max(element.RowSpan, 3);
                    element.MinWidth = 4;
                    element.MinHeight = 0;
                }
                else
                {
                    element.RowSpan = 1;
                    element.ColumnSpan = Math.Max(element.ColumnSpan, 4);
                    element.MinHeight = 4;
                    element.MinWidth = 0;
                }

                WriteGridSpins();
            });
        }

        if (element.Kind == UiElementKind.ProgressBar)
            AddColor(Section("Полоса"), "Заливка", element.Foreground, false, true, (color, _) => element.Foreground = color);

        if (element.Kind == UiElementKind.TextureRect)
        {
            var image = Section("Картинка");
            AddLine(image, "Путь", element.TexturePath, value => element.TexturePath = value);
            var picked = element;
            image.AddChild(MakeButton("Выбрать текстуру", () => OpenTextureBrowser(picked)));
            if (element.SpriteState.Length > 0)
            {
                image.AddChild(new Label
                {
                    Text = element.SpriteState + ", кадр " + (element.SpriteFrame + 1),
                });
            }
            AddChoice(image, "Режим", element.Stretch, new (TextureRect.StretchMode, string)[]
            {
                (TextureRect.StretchMode.Scale, "Растянуть"),
                (TextureRect.StretchMode.Keep, "Как есть"),
                (TextureRect.StretchMode.KeepCentered, "По центру"),
                (TextureRect.StretchMode.KeepAspect, "Пропорции"),
                (TextureRect.StretchMode.KeepAspectCentered, "Пропорции по центру"),
                (TextureRect.StretchMode.KeepAspectCovered, "Заполнить"),
                (TextureRect.StretchMode.Tile, "Плитка"),
            }, value => element.Stretch = value);
        }

        var view = Section("Вид");
        AddCheck(view, "Виден", element.Visible, value => element.Visible = value);
        AddColor(view, "Оттенок", element.Modulate, false, true, (color, _) => element.Modulate = color);
        FillEvents(element);
        ApplyInspectorPage();
    }

    private void FillEvents(UiLayoutElement element)
    {
        var spec = UiElementKinds.Get(element.Kind);
        _events.AddChild(new Label { Text = element.Name });
        var pressed = element.PrimaryEvent.Length > 0 ? element.PrimaryEvent : spec.PressedEvent;
        if (pressed != null && (element.Kind != UiElementKind.TextureRect || element.RaisesPressed))
            AddHook(_events, pressed, element.Action, value => element.Action = value);

        if (spec.ChangedEvent != null)
            AddHook(_events, spec.ChangedEvent, element.ChangedAction, value => element.ChangedAction = value);

        if (element.ActionNote.Length > 0)
            _events.AddChild(new Label { Text = element.ActionNote });
    }

    private void AddChildren(UiLayoutElement element)
    {
        var box = Section("Внутри");
        var any = false;
        foreach (var child in _document.Elements.Where(item => item.ParentId == element.Id).OrderBy(item => item.Order).ThenBy(item => item.Id))
        {
            any = true;
            var id = child.Id;
            var title = child.Name.Length == 0 ? child.Kind.ToString() : child.Name;
            box.AddChild(MakeButton(title, () =>
            {
                _surface.SelectId(id);
                RebuildInspector();
            }));
        }

        if (!any)
            _inspector.RemoveChild(box);
    }

    private void Nudge(UiLayoutElement element, int direction)
    {
        var siblings = _document.Elements
            .Where(item => item.ParentId == element.ParentId)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id)
            .ToList();
        var index = siblings.FindIndex(item => item.Id == element.Id);
        var next = index + direction;
        if (index < 0 || next < 0 || next >= siblings.Count)
            return;

        (siblings[index].Order, siblings[next].Order) = (siblings[next].Order, siblings[index].Order);
        if (siblings[index].Order == siblings[next].Order)
            siblings[next].Order += direction;

        _surface.Remount();
        RebuildInspector();
    }

    private void AddParent(Control parent, UiLayoutElement element)
    {
        var choices = new List<(int Value, string Title)> { (0, "На странице") };
        foreach (var box in _document.Elements)
        {
            if (box.Kind is not (UiElementKind.Box or UiElementKind.Grid) || box.Id == element.Id || ContainsParent(box, element.Id))
                continue;

            var kind = box.Kind == UiElementKind.Grid ? "Сетка" : "Блок";
            var name = box.Name.Length == 0 ? kind + " " + box.Id : box.Name;
            choices.Add((box.Id, kind + ": " + name));
        }

        AddChoice(parent, "Где", element.ParentId, choices.ToArray(), value =>
        {
            if (value == element.ParentId)
                return;

            var previous = element.ParentId;
            element.ParentId = value;
            if (value == 0 && UiLayoutMath.Overlaps(_document, element, element.Column, element.Row, element.ColumnSpan, element.RowSpan))
            {
                element.ParentId = previous;
                SetStatus("Клетка занята.");
                return;
            }

            if (value != 0)
            {
                element.Order = 0;
                foreach (var sibling in _document.Elements)
                {
                    if (sibling.ParentId == value && !ReferenceEquals(sibling, element))
                        element.Order = Math.Max(element.Order, sibling.Order + 1);
                }
            }

            _surface.Remount();
        });
    }

    private bool ContainsParent(UiLayoutElement element, int ancestorId)
    {
        var parentId = element.ParentId;
        while (parentId != 0)
        {
            if (parentId == ancestorId)
                return true;

            var parent = _document.Elements.FirstOrDefault(item => item.Id == parentId);
            if (parent == null)
                return false;

            parentId = parent.ParentId;
        }

        return false;
    }

    private BoxContainer Section(string title)
    {
        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        box.AddChild(new Label
        {
            Text = title,
            FontColorOverride = new Color(0.55f, 0.78f, 1f),
            Margin = new Thickness(0, 8, 0, 2),
        });
        _inspector.AddChild(box);
        return box;
    }

    private void AddLine(Control parent, string title, string value, Action<string> apply)
    {
        var edit = new LineEdit { Text = value, HorizontalExpand = true };
        edit.OnTextChanged += _ =>
        {
            if (_suppress)
                return;

            apply(edit.Text);
            Touch();
        };
        Labeled(parent, title, edit);
    }

    private void AddHook(Control parent, string title, string value, Action<string> apply)
    {
        var edit = new LineEdit
        {
            Text = value,
            HorizontalExpand = true,
        };
        edit.OnTextChanged += _ =>
        {
            if (_suppress)
                return;

            apply(edit.Text);
            Touch();
        };
        Labeled(parent, title, edit);
    }

    private SpinBox AddGrid(Control parent, string title, int value)
    {
        var spin = new SpinBox { HorizontalExpand = true };
        spin.InitDefaultButtons();
        spin.Value = value;
        spin.ValueChanged += _ => CommitGrid();
        Labeled(parent, title, spin);
        return spin;
    }

    private void AddInt(Control parent, string title, int value, Action<int> apply)
    {
        var spin = new SpinBox { HorizontalExpand = true };
        spin.InitDefaultButtons();
        spin.Value = value;
        spin.ValueChanged += args =>
        {
            if (_suppress)
                return;

            apply(args.Value);
            Touch();
        };
        Labeled(parent, title, spin);
    }

    private void AddFloat(Control parent, string title, float value, Action<float> apply)
    {
        var edit = new LineEdit
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            HorizontalExpand = true,
        };
        void Commit()
        {
            if (_suppress)
                return;

            if (!float.TryParse(edit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return;

            apply(number);
            Touch();
        }

        edit.OnTextEntered += _ => Commit();
        edit.OnFocusExit += _ => Commit();
        Labeled(parent, title, edit);
    }

    private void AddCheck(Control parent, string title, bool value, Action<bool> apply)
    {
        var check = new CheckBox { Text = title, Pressed = value };
        check.OnToggled += args =>
        {
            if (_suppress)
                return;

            apply(args.Pressed);
            Touch();
        };
        parent.AddChild(check);
    }

    private void AddChoice<T>(Control parent, string title, T current, (T Value, string Title)[] choices, Action<T> apply)
        where T : struct
    {
        var button = new OptionButton { HorizontalExpand = true };
        var selected = 0;
        for (var i = 0; i < choices.Length; i++)
        {
            button.AddItem(choices[i].Title, i);
            if (EqualityComparer<T>.Default.Equals(choices[i].Value, current))
                selected = i;
        }

        button.TrySelectId(selected);
        button.OnItemSelected += args =>
        {
            if (_suppress || args.Id < 0 || args.Id >= choices.Length)
                return;

            apply(choices[args.Id].Value);
            Touch();
        };
        Labeled(parent, title, button);
    }

    private void AddFont(Control parent, UiLayoutElement element)
    {
        var ids = new List<string>();
#pragma warning disable CS0618
        foreach (var font in _prototypes.EnumeratePrototypes<FontPrototype>())
            ids.Add(font.ID);
#pragma warning restore CS0618
        ids.Sort(StringComparer.Ordinal);

        var button = new OptionButton { HorizontalExpand = true };
        button.AddItem("Тема", 0);
        var selected = 0;
        for (var i = 0; i < ids.Count; i++)
        {
            button.AddItem(ids[i], i + 1);
            if (ids[i] == element.FontId)
                selected = i + 1;
        }

        button.TrySelectId(selected);
        button.OnItemSelected += args =>
        {
            if (_suppress)
                return;

            element.FontId = args.Id <= 0 || args.Id > ids.Count ? "" : ids[args.Id - 1];
            Touch();
        };
        Labeled(parent, "Шрифт", button);
    }

    private void AddColor(
        Control parent,
        string title,
        Color color,
        bool optional,
        bool custom,
        Action<Color, bool> apply)
    {
        var row = new UiColorRow(title, optional);
        row.SetColor(color, !optional || custom);
        row.UserChanged += () =>
        {
            apply(row.Color, row.Custom);
            Touch();
        };
        row.Eyedropper += () => BeginPick(row);
        parent.AddChild(row);
    }

    private void CommitGrid()
    {
        if (_suppress || _column == null || _row == null || _columnSpan == null || _rowSpan == null)
            return;

        if (!_surface.TrySetGrid(_column.Value, _row.Value, _columnSpan.Value, _rowSpan.Value))
            SetStatus("Клетка занята.");

        WriteGridSpins();
    }

    private void WriteGridSpins()
    {
        var element = _surface.Selected;
        if (element == null || _column == null || _row == null || _columnSpan == null || _rowSpan == null)
            return;

        _suppress = true;
        _column.Value = element.Column;
        _row.Value = element.Row;
        _columnSpan.Value = element.ColumnSpan;
        _rowSpan.Value = element.RowSpan;
        _suppress = false;
    }

    private void Touch()
    {
        if (_suppress)
            return;

        _surface.SyncSelected();
    }

    private static void Labeled(Control parent, string title, Control editor)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        row.AddChild(new Label
        {
            Text = title,
            MinSize = new Vector2(118, 0),
            VerticalAlignment = Control.VAlignment.Center,
        });
        editor.HorizontalExpand = true;
        row.AddChild(editor);
        parent.AddChild(row);
    }

    private void BeginPick(UiColorRow row)
    {
        CancelPick();
        _catcher = new EyedropperCatcher();
        _catcher.OnPick += local =>
        {
            var catcher = _catcher;
            if (catcher == null)
                return;

            var scale = catcher.UIScale <= 0 ? 1 : catcher.UIScale;
            var x = (int)(local.X * scale);
            var y = (int)(local.Y * scale);
            CancelPick();
            _clyde.Screenshot(ScreenshotType.Final, image =>
            {
                Color? picked = null;
                try
                {
                    if (image.Width > 0 && image.Height > 0)
                        picked = ReadPixel(image, x, y, row.Color.A);
                }
                finally
                {
                    image.Dispose();
                }

                var color = picked;
                _tasks.RunOnMainThread(() =>
                {
                    if (color == null)
                        return;

                    row.ApplyPicked(color.Value);
                });
            });
        };
        _catcher.OnCancel += CancelPick;
        LayoutContainer.SetAnchorPreset(_catcher, LayoutContainer.LayoutPreset.Wide);
        _ui.RootControl.AddChild(_catcher);
    }

    private void CancelPick()
    {
        if (_catcher == null)
            return;

        _catcher.Orphan();
        _catcher = null;
    }

    private Vector2 MouseUi()
    {
        return _ui.ScreenToUIPosition(_input.MouseScreenPosition).Position;
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }

    private void CommitTabName()
    {
        if (_suppress)
            return;

        _document.Current.Name = _tabName.Text.Length == 0 ? "Вкладка" : _tabName.Text;
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        _tabs.RemoveAllChildren();
        for (var i = 0; i < _document.Tabs.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Text = _document.Tabs[i].Name,
                Disabled = index == _document.ActiveTab,
            };
            button.OnPressed += _ => ShowTab(index);
            _tabs.AddChild(button);
        }

        _suppress = true;
        _tabName.Text = _document.Current.Name;
        _suppress = false;
    }

    private void ShowTab(int index)
    {
        _document.ActiveTab = Math.Clamp(index, 0, _document.Tabs.Count - 1);
        _surface.Reload();
        RefreshTabs();
    }

    private void AddTab()
    {
        _document.Tabs.Add(new UiLayoutTab { Name = "Вкладка " + (_document.Tabs.Count + 1) });
        ShowTab(_document.Tabs.Count - 1);
    }

    private void CloseTab()
    {
        if (_document.Tabs.Count <= 1)
        {
            SetStatus("Последнюю вкладку не убрать.");
            return;
        }

        _document.Tabs.RemoveAt(_document.ActiveTab);
        _document.ActiveTab = Math.Clamp(_document.ActiveTab, 0, _document.Tabs.Count - 1);
        ShowTab(_document.ActiveTab);
    }

    private void OpenGameWindows()
    {
        var dialog = new DefaultWindow
        {
            Title = "Окна игры",
            MinSize = new Vector2(440, 520),
            SetSize = new Vector2(520, 680),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(6),
            SeparationOverride = 6,
        };
        var filter = new LineEdit
        {
            HorizontalExpand = true,
        };
        var count = new Label();
        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };
        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        void Fill()
        {
            list.RemoveAllChildren();
            var query = filter.Text.Trim();
            var shown = 0;
            var windows = UiGameWindowFinder.Load(_types);
            foreach (var entry in windows)
            {
                if (query.Length > 0 &&
                    entry.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.TypeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.FullName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                shown++;
                var caption = entry.Title == entry.TypeName
                    ? entry.TypeName
                    : entry.Title + "   " + entry.TypeName;
                var button = new Button { Text = caption, HorizontalExpand = true };
                var type = entry.Type;
                var name = entry.Title;
                button.OnPressed += _ =>
                {
                    dialog.Close();
                    ImportGameWindow(name, type);
                };
                list.AddChild(button);
            }

            count.Text = shown + " из " + windows.Count;
        }

        filter.OnTextChanged += _ => Fill();
        Fill();
        root.AddChild(filter);
        root.AddChild(count);
        scroll.AddChild(list);
        root.AddChild(scroll);
        dialog.Contents.AddChild(root);
        dialog.Open();
    }

    private void OpenTextureBrowser(UiLayoutElement element)
    {
        var browser = new UiTextureBrowserWindow();
        browser.OnSelected += choice =>
        {
            element.TexturePath = choice.Path;
            element.SpriteState = choice.State;
            element.SpriteDirection = choice.Direction;
            element.SpriteFrame = choice.Frame;
            element.LiveTexture = null;
            if (ReferenceEquals(_surface.Selected, element))
            {
                _surface.SyncSelected();
                RebuildInspector();
            }
        };
        browser.Open();
    }

    private void ImportGameWindow(string title, Type type)
    {
        BaseWindow window;
        try
        {
            window = (BaseWindow) _types.CreateInstance(type, oneOff: true);
        }
        catch (Exception)
        {
            SetStatus("Это окно не собралось.");
            return;
        }

        window.Modulate = new Color(1, 1, 1, 0);
        _ui.RootControl.AddChild(window);
        try
        {
            var size = window.SetSize;
            if (float.IsNaN(size.X) || size.X < 40)
                size.X = 720;
            if (float.IsNaN(size.Y) || size.Y < 40)
                size.Y = 480;

            window.Measure(size);
            var arranged = window.DesiredSize;
            if (arranged.X < 40)
                arranged.X = size.X;
            if (arranged.Y < 40)
                arranged.Y = size.Y;

            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, arranged));
            var fresh = _document.Tabs.Count == 1 && _document.Tabs[0].Elements.Count == 0;
            if (fresh)
            {
                _document.Columns = 24;
                _document.Rows = 16;
            }

            _document.Title = title;
            _document.TitleLoc = UiLocCatalog.FindId(_resources, _loc, title) ?? "";
            _document.Fancy = window is Content.Client.UserInterface.Controls.FancyWindow;
            _document.Resizable = window.Resizable;
            _document.PreviewWidth = arranged.X + 16;
            _document.PreviewHeight = arranged.Y + 64;
            UiLayoutImporter.Import(window, _document, title, () =>
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, arranged)), _resources, _loc);
            FillHeader();
            _surface.Reload();
            RefreshTabs();
            SetStatus("Окно разобрано на сетку вкладки.");
        }
        catch (Exception)
        {
            SetStatus("Окно открылось, но сетка из него не собралась.");
        }
        finally
        {
            window.Orphan();
        }
    }

    private static PanelContainer SolidPanel(float width)
    {
        return new PanelContainer
        {
            MinSize = new Vector2(width, 0),
            HorizontalExpand = width <= 0,
            VerticalExpand = width > 0,
            MouseFilter = MouseFilterMode.Stop,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = new Color(0.09f, 0.1f, 0.13f, 1f),
                BorderColor = new Color(0.35f, 0.4f, 0.48f, 1f),
                BorderThickness = new Thickness(1),
            },
        };
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.OnPressed += _ => action();
        return button;
    }

    private static Color? ReadPixel(SixLabors.ImageSharp.Image image, int x, int y, float alpha)
    {
        var boundedX = Math.Clamp(x, 0, Math.Max(0, image.Width - 1));
        var boundedY = Math.Clamp(y, 0, Math.Max(0, image.Height - 1));
        using var cropped = image.Clone(context =>
            context.Crop(new SixLabors.ImageSharp.Rectangle(boundedX, boundedY, 1, 1)));
        using var stream = new System.IO.MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(cropped, stream);
        var bytes = stream.ToArray();
        if (bytes.Length < 14)
            return null;

        var offset = System.BitConverter.ToInt32(bytes, 10);
        if (offset < 0 || offset + 2 >= bytes.Length)
            return null;

        return new Color(bytes[offset + 2] / 255f, bytes[offset + 1] / 255f, bytes[offset] / 255f, alpha);
    }

    private static string StripExtension(string fileName)
    {
        const string extension = ".yml";
        if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            return fileName[..^extension.Length];

        return fileName;
    }

    private static string Sanitize(string name)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in name.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
                builder.Append(character);
            else if (char.IsWhiteSpace(character))
                builder.Append('_');
        }

        var result = builder.ToString();
        return result.Length == 0 ? "Окно" : result;
    }

    private sealed class PaletteButton : Button
    {
        public UiElementKind Kind;
        public Func<Vector2>? MouseUi;
        public Action<UiElementKind>? Pick;
        public Action<UiElementKind, Vector2>? Drop;
        private bool _held;
        private Vector2 _start;

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _held = true;
            _start = MouseUi?.Invoke() ?? GlobalPosition;
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);
            if (!_held || args.Function != EngineKeyFunctions.UIClick)
                return;

            _held = false;
            var point = MouseUi?.Invoke() ?? GlobalPosition + args.RelativePosition;
            if ((point - _start).Length() > 6)
                Drop?.Invoke(Kind, point);
            else
                Pick?.Invoke(Kind);
        }
    }

    private sealed class EyedropperCatcher : Control
    {
        public event Action<Vector2>? OnPick;
        public event Action? OnCancel;

        public EyedropperCatcher()
        {
            MouseFilter = MouseFilterMode.Stop;
            HorizontalExpand = true;
            VerticalExpand = true;
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                args.Handle();
                OnPick?.Invoke(args.RelativePosition);
            }
            else if (args.Function == EngineKeyFunctions.UIRightClick)
            {
                args.Handle();
                OnCancel?.Invoke();
            }
        }
    }
}
