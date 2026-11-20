// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.DeviceLinkVis;
using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client.DeadSpace.DeviceLinkVis;

public sealed partial class DeviceLinkVisSystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private DeviceLinkVisOverlay? _overlay;

    public bool Enabled { get; private set; }

    public List<DeviceLinkVisLink> Links { get; private set; } = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DeviceLinkVisStateEvent>(OnState);
        _admin.AdminStatusUpdated += OnAdminStatus;
    }

    public override void Shutdown()
    {
        _admin.AdminStatusUpdated -= OnAdminStatus;
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);

        base.Shutdown();
    }

    public bool CanSee()
    {
        return _admin.HasFlag(AdminFlags.Admin)
            || _admin.HasFlag(AdminFlags.Mapping)
            || _admin.HasFlag(AdminFlags.Debug);
    }

    public void Toggle()
    {
        SetEnabled(!Enabled);
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return;

        if (enabled && !CanSee())
            return;

        Enabled = enabled;
        if (!enabled)
        {
            Links = new List<DeviceLinkVisLink>();
            if (_overlay != null)
            {
                _overlays.RemoveOverlay(_overlay);
                _overlay = null;
            }
        }
        else
        {
            _overlay = new DeviceLinkVisOverlay(this, EntityManager);
            _overlays.AddOverlay(_overlay);
        }

        RaiseNetworkEvent(new DeviceLinkVisEnableEvent { Enabled = enabled });
    }

    private void OnState(DeviceLinkVisStateEvent ev)
    {
        if (!Enabled)
            return;

        Links = ev.Links;
    }

    private void OnAdminStatus()
    {
        if (Enabled && !CanSee())
            SetEnabled(false);
    }
}

public sealed partial class ShowDeviceLinksCommand : LocalizedEntityCommands
{
    [Dependency] private DeviceLinkVisSystem _vis = default!;

    public override string Command => "showdevicelinks";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_vis.CanSee())
        {
            shell.WriteError(Loc.GetString("cmd-showdevicelinks-denied"));
            return;
        }

        _vis.Toggle();
        shell.WriteLine(Loc.GetString(_vis.Enabled ? "cmd-showdevicelinks-on" : "cmd-showdevicelinks-off"));
    }
}
