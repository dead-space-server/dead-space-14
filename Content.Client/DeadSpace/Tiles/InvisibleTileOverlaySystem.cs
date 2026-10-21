// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Tiles;

public sealed class InvisibleTileOverlaySystem : EntitySystem
{
    public const string TilePrototype = "FloorInvisible";

    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private InvisibleTileOverlay _overlay = default!;

    public bool Enabled
    {
        get => _overlay.Enabled;
        set => _overlay.Enabled = value;
    }

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new InvisibleTileOverlay
        {
            TileTypeId = _prototypes.Index<ContentTileDefinition>(TilePrototype).TileId,
        };
        _overlays.AddOverlay(_overlay);
        _admin.AdminStatusUpdated += OnAdminStatusUpdated;
    }

    public override void Shutdown()
    {
        _admin.AdminStatusUpdated -= OnAdminStatusUpdated;
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    public bool CanSee()
    {
        return _admin.HasFlag(AdminFlags.Admin) || _admin.HasFlag(AdminFlags.Mapping);
    }

    private void OnAdminStatusUpdated()
    {
        if (!CanSee())
            Enabled = false;
    }
}
