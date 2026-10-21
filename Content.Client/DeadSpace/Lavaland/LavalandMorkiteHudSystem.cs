// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Shared.Atmos.Components;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Small panel in the top right corner while there is a morkite objective on lavaland:
/// the well bar, pipeline integrity and whether morkite is flowing.
/// The cross closes it; a click on the bluespace well, an extractor or a pipe of their network opens it again.
/// </summary>
public sealed class LavalandMorkiteHudSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan HideAfter = TimeSpan.FromSeconds(3);

    /// <summary>Entities the server may accept for reopening; pipes are recognised by their component.</summary>
    private static readonly HashSet<string> ReopenPrototypes = new() { "LavalandBluespaceWell", "LavalandMorkiteExtractor" };

    private bool _closed;

    private PanelContainer? _panel;
    private ProgressBar _morkiteBar = default!;
    private Label _morkiteLabel = default!;
    private ProgressBar _integrityBar = default!;
    private Label _integrityLabel = default!;
    private Label _linkLabel = default!;
    private TimeSpan _lastUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandMorkiteHudEvent>(OnHud);
        SubscribeNetworkEvent<LavalandMorkiteHudReopenEvent>(OnReopen);

        // Does not consume the click: the usual interaction still happens.
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnClick, false, true), typeof(SharedInteractionSystem))
            .BindBefore(ContentKeyFunctions.ActivateItemInWorld, new PointerInputCmdHandler(OnClick, false, true), typeof(SharedInteractionSystem))
            .Register<LavalandMorkiteHudSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<LavalandMorkiteHudSystem>();
        base.Shutdown();
        _panel?.Orphan();
        _panel = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_panel is { Visible: true } && _timing.CurTime - _lastUpdate > HideAfter)
            _panel.Visible = false;
    }

    private bool OnClick(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!_closed || args.State != BoundKeyState.Down || !args.EntityUid.IsValid())
            return false;

        var uid = args.EntityUid;
        if (HasComp<PipeAppearanceComponent>(uid) ||
            MetaData(uid).EntityPrototype?.ID is { } id && ReopenPrototypes.Contains(id))
        {
            RaiseNetworkEvent(new LavalandMorkiteHudReopenRequestEvent(GetNetEntity(uid)));
        }

        return false;
    }

    private void OnReopen(LavalandMorkiteHudReopenEvent ev)
    {
        _closed = false;
        if (_panel != null && _timing.CurTime - _lastUpdate <= HideAfter)
            _panel.Visible = true;
    }

    private void OnHud(LavalandMorkiteHudEvent ev)
    {
        EnsurePanel();
        _panel!.Visible = !_closed;
        _lastUpdate = _timing.CurTime;

        _morkiteBar.Value = ev.Morkite;
        _morkiteLabel.Text = Loc.GetString("lavaland-morkite-hud-morkite", ("percent", (int) Math.Floor(ev.Morkite)));
        _integrityBar.Value = ev.Integrity;
        _integrityLabel.Text = Loc.GetString("lavaland-morkite-hud-integrity", ("percent", (int) Math.Ceiling(ev.Integrity)));
        _integrityLabel.FontColorOverride = ev.Integrity > 60f ? Color.White : ev.Integrity > 25f ? Color.Orange : Color.Red;

        (_linkLabel.Text, _linkLabel.FontColorOverride) = ev.Link switch
        {
            LavalandMorkiteLink.Connected => (Loc.GetString("lavaland-morkite-hud-connected"), Color.FromHex("#4fe0a8")),
            LavalandMorkiteLink.Leaking => (Loc.GetString("lavaland-morkite-hud-leaking"), Color.Red),
            LavalandMorkiteLink.NotConnected => (Loc.GetString("lavaland-morkite-hud-not-connected"), Color.Orange),
            _ => (Loc.GetString("lavaland-morkite-hud-no-well"), Color.Gray),
        };
    }

    private void EnsurePanel()
    {
        if (_panel != null)
            return;

        _morkiteLabel = new Label { FontColorOverride = Color.FromHex("#9be8ff") };
        _morkiteBar = new ProgressBar { MinValue = 0, MaxValue = 100, MinSize = new Vector2(170, 8) };
        _integrityLabel = new Label();
        _integrityBar = new ProgressBar { MinValue = 0, MaxValue = 100, MinSize = new Vector2(170, 8) };
        _linkLabel = new Label();

        var closeButton = new Button
        {
            Text = "✕",
            ToolTip = Loc.GetString("lavaland-morkite-hud-close"),
            MinSize = new Vector2(20, 20),
        };
        closeButton.OnPressed += _ =>
        {
            _closed = true;
            if (_panel != null)
                _panel.Visible = false;
        };

        _panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0b1310").WithAlpha(0.85f),
                BorderColor = Color.FromHex("#b96b2d"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 6,
            },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            Children =
                            {
                                new Label
                                {
                                    Text = Loc.GetString("lavaland-morkite-hud-title"),
                                    FontColorOverride = Color.FromHex("#ffb56b"),
                                    HorizontalExpand = true,
                                },
                                closeButton,
                            },
                        },
                        _morkiteLabel,
                        _morkiteBar,
                        _integrityLabel,
                        _integrityBar,
                        _linkLabel,
                    },
                },
            },
        };

        _ui.WindowRoot.AddChild(_panel);
        LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.TopRight);
        LayoutContainer.SetMarginTop(_panel, 110);
        LayoutContainer.SetMarginRight(_panel, -12);
        LayoutContainer.SetGrowHorizontal(_panel, LayoutContainer.GrowDirection.Begin);
    }
}
