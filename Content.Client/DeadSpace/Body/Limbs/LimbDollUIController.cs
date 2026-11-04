// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Surgery;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbDollUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private static readonly Color Normal = new(0.45f, 0.72f, 0.82f);
    private static readonly Color Gone = new(0.42f, 0.44f, 0.47f);
    private static readonly Color Clear = new(0.66f, 0.7f, 0.74f);

    private static readonly LimbZone[] RingOrder =
    {
        LimbZone.Head, LimbZone.Eyes, LimbZone.Mouth,
        LimbZone.ArmLeft, LimbZone.HandLeft, LimbZone.LegLeft, LimbZone.FootLeft,
        LimbZone.Groin,
        LimbZone.FootRight, LimbZone.LegRight, LimbZone.HandRight, LimbZone.ArmRight,
        LimbZone.Torso,
    };

    private LimbDollHud? _hud;
    private LimbHealthView? _health;
    private LimbAimView? _aim;
    private SurgeryFan? _menu;
    private Vector2 _menuAt;
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
        {
            ShowDoll();
            if (_hud == null)
                return;
        }

        if (_menu != null && (_input.MouseScreenPosition.Position - _menuAt).Length() > (_menu.MinSize.X * 0.5f + 48f) * UiScale())
            CloseMenu();

        if (_health == null || _health.Parent == null || _aim == null || _aim.Parent == null)
            PlaceHealth();
        else
            PinAlerts();

        PinDoll();
        _hud.Doll.Selected = Current();
        if (_health != null)
        {
            _health.Zones = Body();
            _health.Pools = Pools();
        }
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
        _aim.Pressed += _ => ToggleAim();
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
        if (_menu != null)
        {
            CloseMenu();
            return;
        }

        if (!LimbTuning.Enabled)
            return;

        var selected = Current();
        var body = Body();
        var icons = new List<SurgeryFanIcon>();
        var picks = new List<LimbZone?>();
        foreach (var zone in RingOrder)
        {
            if (!LimbDollLayout.TryPart(zone, out var part))
                continue;

            var lost = Lost(body, zone);
            var color = selected == zone ? LimbDollPalette.Picked : lost ? Gone : Normal;
            var name = lost
                ? Loc.GetString("limb-doll-lost", ("part", Loc.GetString(part.Short)))
                : Loc.GetString(part.Name);
            icons.Add(new SurgeryFanIcon(new List<Texture>(), SurgeryFanBadge.None, color, name, Loc.GetString(part.Short)));
            picks.Add(zone);
        }

        icons.Add(new SurgeryFanIcon(new List<Texture>(), SurgeryFanBadge.None, Clear, Loc.GetString("limb-doll-clear-ring"), Loc.GetString(LimbDollLayout.ClearShort)));
        picks.Add(null);

        var fan = new SurgeryFan();
        fan.SetIcons(icons);
        fan.Picked += index =>
        {
            if ((uint) index < (uint) picks.Count)
                Choose(picks[index]);
            CloseMenu();
        };

        UIManager.WindowRoot.AddChild(fan);
        LayoutContainer.SetAnchorPreset(fan, LayoutContainer.LayoutPreset.TopLeft);
        _menuAt = _input.MouseScreenPosition.Position;
        var at = _menuAt / UiScale();
        LayoutContainer.SetMarginLeft(fan, at.X - fan.MinSize.X * fan.Origin.X);
        LayoutContainer.SetMarginTop(fan, at.Y - fan.MinSize.Y * fan.Origin.Y);
        _menu = fan;
    }

    private static bool Lost(IReadOnlyList<LimbZoneState>? body, LimbZone zone)
    {
        if (body == null || !LimbWoundRules.IsBody(zone))
            return false;

        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) body.Count && body[index].Severed;
    }

    private float UiScale()
    {
        return MathF.Max(0.01f, UIManager.RootControl.UIScale);
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
        {
            _health.Zones = Body();
            _health.Pools = Pools();
        }
    }

    private void ToggleAim()
    {
        if (_player.LocalEntity is not { } uid || !_entities.TryGetComponent(uid, out LimbAimComponent? aim))
            return;

        var next = !aim.Aiming;
        _entities.RaisePredictiveEvent(new LimbAimSetEvent(next));
        _entities.System<LimbWoundSystem>().SetAiming(uid, next);
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
        return Pools()?.Zones;
    }

    private LimbBodyComponent? Pools()
    {
        if (_player.LocalEntity is not { } uid || !_entities.TryGetComponent(uid, out LimbBodyComponent? body))
            return null;

        return body;
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        var menu = _menu;
        _menu = null;
        menu.Orphan();
    }
}
