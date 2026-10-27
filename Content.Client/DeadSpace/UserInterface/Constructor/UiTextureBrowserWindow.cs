// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiTextureBrowserWindow : DefaultWindow
{
    private static readonly ResPath TextureRoot = new("/Textures");
    private static readonly string[] PictureExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    public event Action<UiTextureChoice>? OnSelected;

    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IResourceManager _resources = default!;

    private readonly LineEdit _filter = new()
    {
        PlaceHolder = "Фильтр по имени",
        HorizontalExpand = true,
    };
    private readonly Label _path = new() { HorizontalExpand = true };
    private readonly BoxContainer _list = new()
    {
        Orientation = LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 2,
    };
    private readonly TextureRect _preview = new()
    {
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        MinSize = new Vector2(140, 140),
        SetSize = new Vector2(140, 140),
        HorizontalExpand = true,
        VerticalExpand = true,
    };

    private readonly BoxContainer _frames = new()
    {
        Orientation = LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 4,
    };

    private ResPath _directory = TextureRoot;
    private string _selected = "";
    private string _state = "";
    private int _direction;
    private int _frame;

    public UiTextureBrowserWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = "Текстуры";
        MinSize = new Vector2(520, 560);
        SetSize = new Vector2(640, 720);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(6),
            SeparationOverride = 6,
        };

        var bar = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        bar.AddChild(Make("Вверх", Up));
        bar.AddChild(Make("Текстуры", () => OpenDirectory(TextureRoot)));
        bar.AddChild(_path);
        root.AddChild(bar);
        root.AddChild(_filter);

        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(280, 0),
        };
        scroll.AddChild(_list);
        body.AddChild(scroll);

        var side = new PanelContainer
        {
            MinSize = new Vector2(160, 0),
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Stop,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = new Color(0.09f, 0.1f, 0.13f, 1f),
                BorderColor = new Color(0.35f, 0.4f, 0.48f, 1f),
                BorderThickness = new Thickness(1),
            },
        };
        var sideBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(6),
        };
        sideBox.AddChild(new Label { Text = "Просмотр" });
        sideBox.AddChild(_preview);
        sideBox.AddChild(_frames);
        sideBox.AddChild(Make("Вставить", Insert));
        side.AddChild(sideBox);
        body.AddChild(side);
        root.AddChild(body);
        Contents.AddChild(root);

        _filter.OnTextChanged += _ => Fill();
        OpenDirectory(TextureRoot);
    }

    private void Up()
    {
        if (_directory == TextureRoot)
            return;

        var text = _directory.ToString();
        var slash = text.LastIndexOf('/');
        if (slash <= 0)
        {
            OpenDirectory(TextureRoot);
            return;
        }

        var parent = text[..slash];
        if (!parent.StartsWith("/Textures", StringComparison.Ordinal))
            parent = "/Textures";

        OpenDirectory(new ResPath(parent));
    }

    private void OpenDirectory(ResPath path)
    {
        _directory = path;
        _path.Text = path.ToString();
        Fill();
    }

    private void Fill()
    {
        _list.RemoveAllChildren();
        if (_directory.ToString().EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
        {
            FillRsi();
            return;
        }

        _frames.RemoveAllChildren();
        _state = "";
        var query = _filter.Text.Trim();
        var folders = new List<string>();
        var files = new List<string>();
        foreach (var entry in _resources.ContentGetDirectoryEntries(_directory))
        {
            if (query.Length > 0 && entry.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (entry.EndsWith('/'))
                folders.Add(entry);
            else if (IsPicture(entry))
                files.Add(entry);
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            var name = folder;
            var button = new Button { Text = name, HorizontalExpand = true };
            button.OnPressed += _ => OpenDirectory(new ResPath(_directory.ToString().TrimEnd('/') + "/" + name.TrimEnd('/')));
            _list.AddChild(button);
        }

        foreach (var file in files)
        {
            var name = file;
            var path = _directory.ToString().TrimEnd('/') + "/" + name;
            var button = new Button { Text = name, HorizontalExpand = true };
            button.OnPressed += _ => ShowPreview(path);
            _list.AddChild(button);
        }

        if (_list.ChildCount == 0)
            _list.AddChild(new Label { Text = "В этой папке нет картинок." });
    }

    private void FillRsi()
    {
        RSI rsi;
        try
        {
            rsi = _cache.GetResource<RSIResource>(_directory).RSI;
        }
        catch (Exception)
        {
            _list.AddChild(new Label { Text = "Этот спрайт не открылся." });
            return;
        }

        foreach (var state in rsi)
        {
            var name = state.StateId.Name ?? "";
            if (name.Length == 0)
                continue;

            var button = new Button { Text = name, HorizontalExpand = true };
            var captured = state;
            button.OnPressed += _ => SelectState(captured);
            _list.AddChild(button);
        }
    }

    private void SelectState(RSI.State state)
    {
        _state = state.StateId.Name ?? "";
        _selected = _directory.ToString();
        _direction = 0;
        _frame = 0;
        _frames.RemoveAllChildren();

        var directions = state.Icons.Length;
        if (directions > 1)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 2,
            };
            for (var i = 0; i < directions && i < DirectionNames.Length; i++)
            {
                var index = i;
                var button = new Button { Text = DirectionNames[i] };
                button.OnPressed += _ =>
                {
                    _direction = index;
                    _frame = 0;
                    ShowStateFrame();
                };
                row.AddChild(button);
            }

            _frames.AddChild(row);
        }

        var frames = state.Icons[0].Length;
        if (frames > 1)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 2,
            };
            for (var i = 0; i < frames; i++)
            {
                var index = i;
                var button = new Button { Text = (i + 1).ToString() };
                button.OnPressed += _ =>
                {
                    _frame = index;
                    ShowStateFrame();
                };
                row.AddChild(button);
            }

            _frames.AddChild(row);
        }

        ShowStateFrame();
    }

    private void ShowStateFrame()
    {
        try
        {
            var rsi = _cache.GetResource<RSIResource>(_directory).RSI;
            if (!rsi.TryGetState(new RSI.StateId(_state), out var state))
                return;

            var direction = Math.Clamp(_direction, 0, state.Icons.Length - 1);
            var frame = Math.Clamp(_frame, 0, state.Icons[direction].Length - 1);
            _direction = direction;
            _frame = frame;
            _preview.Texture = state.Icons[direction][frame];
        }
        catch (Exception)
        {
            _preview.Texture = null;
        }
    }

    private static readonly string[] DirectionNames = { "Юг", "Север", "Восток", "Запад", "ЮВ", "ЮЗ", "СВ", "СЗ" };

    private void Insert()
    {
        if (_selected.Length == 0)
            return;

        OnSelected?.Invoke(new UiTextureChoice(_selected, _state, _direction, _frame));
        Close();
    }

    private void ShowPreview(string path)
    {
        _selected = path;
        _state = "";
        _direction = 0;
        _frame = 0;
        _frames.RemoveAllChildren();
        try
        {
            _preview.Texture = _cache.GetTexture(path);
        }
        catch (Exception)
        {
            _preview.Texture = null;
        }
    }

    private static bool IsPicture(string name)
    {
        foreach (var extension in PictureExtensions)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Button Make(string text, Action action)
    {
        var button = new Button { Text = text };
        button.OnPressed += _ => action();
        return button;
    }
}

public readonly record struct UiTextureChoice(string Path, string State, int Direction, int Frame);
