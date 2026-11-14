// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;

    private void ApplyPortal(RuneCastContext ctx, RunePortalEffect effect)
    {
        if (!TryComp<RuneCasterComponent>(ctx.Caster, out var caster))
            return;

        var positions = Positions(ctx, RuneSpawnPattern.AtPoint, 1);
        if (positions.Count == 0)
            return;

        var (coords, blocked) = positions[0];
        if (blocked)
            ClearTile(coords);

        if (caster.PortalPending is not { } first || TerminatingOrDeleted(first))
        {
            DeleteIfAlive(caster.PortalA);
            DeleteIfAlive(caster.PortalB);
            caster.PortalA = null;
            caster.PortalB = null;
            caster.PortalPending = Spawn(effect.Prototype, coords);
            ctx.KeepUltimateReady = true;
            _popup.PopupEntity(Loc.GetString("rune-portal-first"), ctx.Caster, ctx.Caster, PopupType.Medium);
            return;
        }

        var second = Spawn(effect.Prototype, coords);
        _link.TryLink(first, second);
        caster.PortalA = first;
        caster.PortalB = second;
        caster.PortalPending = null;
        _popup.PopupEntity(Loc.GetString("rune-portal-open"), ctx.Caster, ctx.Caster, PopupType.Medium);
    }

    private void ClearTile(EntityCoordinates coords)
    {
        var map = _transform.ToMapCoordinates(coords);
        if (!_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, map);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        var doomed = new List<EntityUid>();
        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<MobStateComponent>(ent) || !TryComp<PhysicsComponent>(ent, out var physics) || !physics.Hard)
                continue;

            doomed.Add(ent.Value);
        }

        foreach (var ent in doomed)
            QueueDel(ent);
    }

    private void DeleteIfAlive(EntityUid? uid)
    {
        if (uid is { } ent && !TerminatingOrDeleted(ent))
            QueueDel(ent);
    }
}
