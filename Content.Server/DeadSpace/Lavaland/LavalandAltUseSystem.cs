// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Alt + right click with a held item that handles it itself: checks and passes it on to the item.</summary>
public sealed class LavalandAltUseSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandAltUseRequestEvent>(OnRequest);
    }

    private void OnRequest(LavalandAltUseRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            GetEntity(ev.Item) is not { Valid: true } item ||
            !TryComp<LavalandAltUseActionComponent>(item, out var altUse) ||
            altUse.Action != null ||
            !_hands.IsHolding(user, item) ||
            !_blocker.CanInteract(user, null))
        {
            return;
        }

        var coordinates = GetCoordinates(ev.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return;

        EntityUid? target = ev.Target is { } netTarget && GetEntity(netTarget) is { Valid: true } targetUid ? targetUid : null;
        var altEvent = new LavalandAltUseEvent(user, coordinates, target);
        RaiseLocalEvent(item, ref altEvent);
    }
}
