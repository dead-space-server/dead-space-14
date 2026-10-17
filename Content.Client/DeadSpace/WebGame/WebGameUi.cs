// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Fragments;
using Content.Shared.DeadSpace.WebGame;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;

namespace Content.Client.DeadSpace.WebGame;

public sealed partial class WebGameUi : UIFragment
{
    private BoxContainer _root = default!;
    private List<WebGameEntry> _games = new();

    public override Control GetUIFragmentRoot()
    {
        return _root;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        if (fragmentOwner != null)
        {
            var entities = IoCManager.Resolve<IEntityManager>();
            if (entities.TryGetComponent(fragmentOwner.Value, out WebGameComponent? game))
                _games = game.Games;
        }

        ShowList();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
    }

    private void ShowList()
    {
        _root.RemoveAllChildren();

        foreach (var game in _games)
        {
            var entry = game;
            var button = new Button
            {
                Text = Loc.GetString(entry.Name),
                HorizontalExpand = true,
            };
            button.OnPressed += _ => ShowGame(entry.Url);
            _root.AddChild(button);
        }
    }

    private void ShowGame(string url)
    {
        _root.RemoveAllChildren();

        IWebViewManager? web = null;
        var ready = IoCManager.Instance != null
                    && IoCManager.Instance.TryResolveType(out web)
                    && web.TryUseBrowser();

        AddBackButton();

        if (!ready)
        {
            _root.AddChild(new Label
            {
                Text = Loc.GetString("web-game-unavailable"),
                HorizontalExpand = true,
            });
            return;
        }

        var view = new WebViewControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(320, 280),
        };
        view.Url = url;
        _root.AddChild(view);
        view.GrabKeyboardFocus();
    }

    private void AddBackButton()
    {
        var back = new Button
        {
            Text = Loc.GetString("web-game-back"),
            HorizontalExpand = true,
        };
        back.OnPressed += _ => ShowList();
        _root.AddChild(back);
    }
}
