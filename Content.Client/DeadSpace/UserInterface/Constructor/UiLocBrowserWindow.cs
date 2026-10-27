// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Globalization;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiLocBrowserWindow : DefaultWindow
{
    public readonly record struct Choice(string Id);

    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IResourceManager _resources = default!;

    public event Action<Choice>? OnSelected;

    private readonly OptionButton _language = new() { MinWidth = 120 };
    private readonly LineEdit _filter = new() { PlaceHolder = "Фильтр", HorizontalExpand = true };
    private readonly Label _preview = new() { HorizontalExpand = true };
    private readonly BoxContainer _list = new()
    {
        Orientation = LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 2,
    };

    private ResPath _directory = new("/Locale");
    private string _picked = "";
    private readonly List<CultureInfo> _cultures = new();

    public UiLocBrowserWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = "Строки";
        MinSize = new System.Numerics.Vector2(640, 480);
        SetSize = new System.Numerics.Vector2(760, 560);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(6),
            SeparationOverride = 6,
        };
        var bar = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        bar.AddChild(_language);
        bar.AddChild(_filter);
        bar.AddChild(Make("Вверх", Up));
        bar.AddChild(Make("Вставить", Insert));
        root.AddChild(bar);
        root.AddChild(_preview);
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        Contents.AddChild(root);

        _cultures.AddRange(_loc.GetFoundCultures());
        for (var i = 0; i < _cultures.Count; i++)
        {
            _language.AddItem(_cultures[i].Name, i);
            if (_loc.DefaultCulture != null && _cultures[i].Name == _loc.DefaultCulture.Name)
                _language.TrySelectId(i);
        }

        _language.OnItemSelected += args =>
        {
            _language.TrySelectId(args.Id);
            if (args.Id < 0 || args.Id >= _cultures.Count)
                return;

            _loc.SetCulture(_cultures[args.Id]);
            _directory = new ResPath("/Locale/" + _cultures[args.Id].Name);
            _picked = "";
            Fill();
        };
        _filter.OnTextChanged += _ => Fill();

        if (_loc.DefaultCulture != null)
            _directory = new ResPath("/Locale/" + _loc.DefaultCulture.Name);
        Fill();
    }

    private void Up()
    {
        var text = _directory.ToString().TrimEnd('/');
        var slash = text.LastIndexOf('/');
        if (slash <= 0)
            return;

        _directory = new ResPath(text[..slash]);
        _picked = "";
        _preview.Text = "";
        Fill();
    }

    private void Insert()
    {
        if (_picked.Length == 0)
            return;

        OnSelected?.Invoke(new Choice(_picked));
        Close();
    }

    private void Fill()
    {
        _list.RemoveAllChildren();
        var query = _filter.Text.Trim();
        if (_directory.ToString().EndsWith(".ftl", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var id in UiLocCatalog.IdsIn(_resources, _directory))
            {
                if (query.Length > 0 && id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var key = id;
                _list.AddChild(Make(key, () =>
                {
                    _picked = key;
                    _preview.Text = _loc.TryGetString(key, out var value) ? value : key;
                }));
            }

            return;
        }

        var folders = new List<string>();
        var files = new List<string>();
        foreach (var entry in _resources.ContentGetDirectoryEntries(_directory))
        {
            if (query.Length > 0 && entry.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (entry.EndsWith('/'))
                folders.Add(entry);
            else if (entry.EndsWith(".ftl", StringComparison.OrdinalIgnoreCase))
                files.Add(entry);
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            var name = folder;
            _list.AddChild(Make(name, () => Open(name.TrimEnd('/'))));
        }

        foreach (var file in files)
        {
            var name = file;
            _list.AddChild(Make(name, () => Open(name)));
        }
    }

    private void Open(string name)
    {
        _directory = new ResPath(_directory.ToString().TrimEnd('/') + "/" + name);
        _picked = "";
        _preview.Text = "";
        Fill();
    }

    private static Button Make(string text, Action action)
    {
        var button = new Button { Text = text, HorizontalExpand = true };
        button.OnPressed += _ => action();
        return button;
    }
}
