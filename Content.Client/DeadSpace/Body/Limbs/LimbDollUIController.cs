// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbDollUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private LimbDollHud? _hud;
    private LimbHealthView? _health;
    private LimbAimView? _aim;
    private SimpleRadialMenu? _menu;
    private bool _alive;

    public override void Initialize()
    {
        base.Initialize();
        var screens = UIManager.GetUIController<GameplayStateLoadController>();
        screens.OnScreenLoad += ShowDoll;
        screens.OnScreenUnload += HideDoll;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (!LimbTuning.Enabled || !_alive)
        {
            if (_hud != null)
                HideDoll();
            return;
        }

        if (_hud == null)
            ShowDoll();

        if (_hud == null)
            return;

        if (_health == null || _health.Parent == null || _aim == null || _aim.Parent == null)
            PlaceHealth();
        else
            PinAlerts();

        PinDoll();
        _hud.Doll.Selected = Current();
        if (_health != null)
            _health.Zones = Body();
        if (_aim != null)
        {
            _aim.Aimed = Aiming();
            _aim.ToolTip = Loc.GetString(_aim.Aimed ? "limb-aim-on" : "limb-aim-off");
        }
    }

    public void OnStateEntered(GameplayState state)
    {
        _alive = true;
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.LimbDoll, InputCmdHandler.FromDelegate(_ => OpenRadial(), _ => { }))
            .Register<LimbDollUIController>();
        ShowDoll();
    }

    public void OnStateExited(GameplayState state)
    {
        _alive = false;
        CommandBinds.Unregister<LimbDollUIController>();
        CloseMenu();
        HideDoll();
    }

    private void ShowDoll()
    {
        HideDoll();
        var host = Viewport(UIManager.ActiveScreen);
        if (host == null)
            return;

        _hud = new LimbDollHud();
        _hud.Doll.OnZone += Choose;
        host.AddChild(_hud);
        LayoutContainer.SetAnchorAndMarginPreset(_hud, LayoutContainer.LayoutPreset.BottomRight, margin: LimbDollPalette.Corner);
        _health = new LimbHealthView();
        _aim = new LimbAimView();
        PlaceHealth();
        RefreshDoll();
    }

    private void HideDoll()
    {
        if (_hud?.Parent != null)
            _hud.Parent.RemoveChild(_hud);

        if (_health?.Parent != null)
            _health.Parent.RemoveChild(_health);

        if (_aim?.Parent != null)
            _aim.Parent.RemoveChild(_aim);

        _hud = null;
        _health = null;
        _aim = null;
    }

    private void PlaceHealth()
    {
        if (_health == null)
            return;

        var alerts = UIManager.GetActiveUIWidgetOrNull<AlertsUI>();
        if (alerts == null)
            return;

        if (_aim != null && _aim.Parent != alerts.AlertContainer)
            alerts.AlertContainer.AddChild(_aim);

        if (_health.Parent != alerts.AlertContainer)
            alerts.AlertContainer.AddChild(_health);

        PinAlerts();
    }

    private void PinAlerts()
    {
        if (_aim?.Parent != null && _aim.GetPositionInParent() != 0)
            _aim.SetPositionInParent(0);

        if (_health?.Parent != null && _health.GetPositionInParent() != 1)
            _health.SetPositionInParent(1);
    }

    private Vector2 _parentSize;

    private void PinDoll()
    {
        if (_hud?.Parent is not { } parent || parent.Size == _parentSize)
            return;

        _parentSize = parent.Size;
        LayoutContainer.SetAnchorAndMarginPreset(_hud, LayoutContainer.LayoutPreset.BottomRight, margin: LimbDollPalette.Corner);
    }

    private static Control? Viewport(Control? root)
    {
        if (root == null)
            return null;

        if (root.Name == "ViewportContainer")
            return root;

        foreach (var child in root.Children)
        {
            var found = Viewport(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private void OpenRadial()
    {
        if (!LimbTuning.Enabled || _menu != null)
            return;

        var selected = Current();
        var options = new List<RadialMenuOptionBase>();
        foreach (var part in LimbDollLayout.Parts)
            options.Add(Option(part, selected));

        options.Add(new RadialMenuActionOption<LimbZone?>(Choose, null)
        {
            ToolTip = Loc.GetString(LimbDollLayout.ClearShort),
            Order = LimbDollLayout.Parts.Length,
            BackgroundColor = LimbDollPalette.Idle,
            HoverBackgroundColor = LimbDollPalette.Hover,
        });

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(options, new SimpleRadialMenuSettings { DefaultContainerRadius = LimbDollPalette.RadialRadius });
        _menu.OnClose += CloseMenu;
        _menu.Open();
        _menu.OpenOverMouseScreenPosition();
    }

    private RadialMenuActionOption<LimbZone?> Option(LimbDollPart part, LimbZone? selected)
    {
        return new RadialMenuActionOption<LimbZone?>(Choose, part.Zone)
        {
            ToolTip = Loc.GetString(part.Short),
            Order = part.Order,
            BackgroundColor = selected == part.Zone ? LimbDollPalette.Picked : LimbDollPalette.Idle,
            HoverBackgroundColor = LimbDollPalette.Hover,
        };
    }

    private void Choose(LimbZone? zone)
    {
        if (_player.LocalEntity is not { } uid)
            return;

        _entities.System<LimbWoundSystem>().SetTarget(uid, zone);
        _entities.RaisePredictiveEvent(new LimbZoneSetEvent(zone));
        RefreshDoll();
    }

    private void RefreshDoll()
    {
        if (_hud == null)
            return;

        PlaceHealth();
        PinDoll();
        _hud.Doll.Selected = Current();
        if (_health != null)
            _health.Zones = Body();
    }

    private bool Aiming()
    {
        return _player.LocalEntity is { } uid
               && _entities.TryGetComponent(uid, out LimbAimComponent? aim)
               && aim.Aiming;
    }

    private LimbZone? Current()
    {
        if (_player.LocalEntity is not { } uid || !_entities.TryGetComponent(uid, out LimbTargetComponent? target))
            return null;

        return target.Selected;
    }

    private IReadOnlyList<LimbZoneState>? Body()
    {
        if (_player.LocalEntity is not { } uid || !_entities.TryGetComponent(uid, out LimbBodyComponent? body))
            return null;

        return body.Zones;
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        var menu = _menu;
        _menu = null;
        menu.Close();
    }
}
