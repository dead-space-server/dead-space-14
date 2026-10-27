// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiLayoutPreviewWindow : DefaultWindow
{
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private readonly UiLayoutDocument _document;
    private readonly UiConstructorSurface _surface;
    private readonly List<CultureInfo> _cultures = new();

    public UiLayoutPreviewWindow(UiLayoutDocument document, UiLayoutFactory factory)
    {
        IoCManager.InjectDependencies(this);
        _document = document;
        Title = TitleText();
        MinSize = new Vector2(320, 220);
        var width = document.PreviewWidth > 40 ? document.PreviewWidth : 760;
        var height = document.PreviewHeight > 40 ? document.PreviewHeight : 520;
        SetSize = new Vector2(width, height);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var bar = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Robust.Shared.Maths.Thickness(4),
        };
        bar.AddChild(Preset("Узкое", 520, 360));
        bar.AddChild(Preset("Среднее", 760, 520));
        bar.AddChild(Preset("Широкое", 1100, 680));

        _surface = new UiConstructorSurface(document, factory, false)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var language = new OptionButton { MinWidth = 120 };
        _cultures.AddRange(_loc.GetFoundCultures());
        for (var i = 0; i < _cultures.Count; i++)
        {
            language.AddItem(_cultures[i].Name, i);
            if (_loc.DefaultCulture != null && _cultures[i].Name == _loc.DefaultCulture.Name)
                language.TrySelectId(i);
        }

        language.OnItemSelected += args =>
        {
            language.TrySelectId(args.Id);
            if (args.Id < 0 || args.Id >= _cultures.Count)
                return;

            _loc.SetCulture(_cultures[args.Id]);
            _surface.Remount();
            Title = TitleText();
        };
        bar.AddChild(language);
        root.AddChild(bar);
        root.AddChild(_surface);
        Contents.AddChild(root);
    }

    private string TitleText()
    {
        if (_document.TitleLoc.Length > 0 && _loc.TryGetString(_document.TitleLoc, out var value))
            return value;

        return _document.Title.Length == 0 ? "Просмотр" : _document.Title;
    }

    private Button Preset(string title, float width, float height)
    {
        var button = new Button { Text = title };
        button.OnPressed += _ => SetSize = new Vector2(width, height);
        return button;
    }
}
