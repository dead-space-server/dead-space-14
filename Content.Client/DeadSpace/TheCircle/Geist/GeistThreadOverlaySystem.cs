// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.TheCircle.Geist;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.TheCircle.Geist;

/// <summary>
/// Draws the geist's thread from its spool to the tied item. The server sends the thread only to the one who tied it;
/// the owner check here keeps it so even if the state is around (a body swap).
/// </summary>
public sealed class GeistThreadOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new GeistThreadOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<GeistThreadOverlay>();
    }
}

public sealed class GeistThreadOverlay : Overlay
{
    private static readonly Color ThreadColor = Color.FromHex("#e6e6e6aa");

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;

    public GeistThreadOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _player = IoCManager.Resolve<IPlayerManager>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var xformSystem = _entManager.System<SharedTransformSystem>();
        var query = _entManager.EntityQueryEnumerator<GeistThreadComponent, TransformComponent>();
        while (query.MoveNext(out var thread, out var xform))
        {
            if (thread.TiedBy != local ||
                thread.Spool is not { } spool ||
                xform.MapID != args.MapId ||
                !_entManager.TryGetComponent(spool, out TransformComponent? spoolXform) ||
                spoolXform.MapID != args.MapId)
            {
                continue;
            }

            args.WorldHandle.DrawLine(xformSystem.GetWorldPosition(spoolXform), xformSystem.GetWorldPosition(xform), ThreadColor);
        }
    }
}
