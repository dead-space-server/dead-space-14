// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Trigger;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System;

namespace Content.Server.DeadSpace.Hooligan.Objectives;

/// <summary>
/// Логика цели на подрыв самодельных бомб
/// Считает взрывы бомб из банок колы в разных отделах станции
/// </summary>
public sealed class HooliganBombConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly EntProtoId FireBombId = "FireBomb";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosiveComponent, TriggerEvent>(OnTriggered);
        SubscribeLocalEvent<HooliganBombConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnTriggered(Entity<ExplosiveComponent> ent, ref TriggerEvent args)
    {
        // Считаем только момент детонации, а не поджиг таймера.
        if (args.Key != "timer")
            return;

        if (args.User is not { } user)
            return;

        if (!IsFireBomb(ent.Owner))
            return;

        if (!_mind.TryGetMind(user, out _, out var mind))
            return;

        var bombCoords = _transform.GetMapCoordinates(ent.Owner);
        var station = _station.GetOwningStation(ent.Owner);

        foreach (var objective in mind.Objectives)
        {
            if (!TryComp<HooliganBombConditionComponent>(objective, out var condition))
                continue;

            if (!TryFindDepartment(bombCoords, station, condition, out var department))
                continue;

            // Бомба в уже использованном отделе не засчитывается.
            if (!condition.DetonatedDepartments.Add(department))
                continue;

            condition.Count++;
        }
    }

    private bool IsFireBomb(EntityUid bomb)
    {
        var protoId = MetaData(bomb).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        if (protoId == FireBombId.Id)
            return true;

        if (!_prototype.HasIndex<EntityPrototype>(protoId))
            return false;

        foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(protoId))
        {
            if (parent.ID == FireBombId.Id)
                return true;
        }

        return false;
    }

    private bool TryFindDepartment(
        MapCoordinates bombCoords,
        EntityUid? station,
        HooliganBombConditionComponent condition,
        out EntProtoId department)
    {
        department = default;
        var found = false;
        var nearestDistance = float.MaxValue;
        var rangeSquared = condition.Range * condition.Range;

        var query = EntityQueryEnumerator<WarpPointComponent>();
        while (query.MoveNext(out var uid, out var warp))
        {
            if (string.IsNullOrWhiteSpace(warp.Location))
                continue;
            if (_station.GetOwningStation(uid) != station)
                continue;
            if (!TryGetDepartment(uid, condition.DepartmentBeacons, out var beaconDepartment))
                continue;

            var warpCoords = _transform.GetMapCoordinates(uid);
            if (warpCoords.MapId != bombCoords.MapId)
                continue;

            var distance = (warpCoords.Position - bombCoords.Position).LengthSquared();
            if (distance > rangeSquared || distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            department = beaconDepartment;
            found = true;
        }

        return found;
    }

    private bool TryGetDepartment(
        EntityUid warp,
        List<EntProtoId> departments,
        out EntProtoId department)
    {
        department = default;
        var protoId = MetaData(warp).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        foreach (var candidate in departments)
        {
            if (candidate.Id == protoId)
            {
                department = candidate;
                return true;
            }
        }

        if (!_prototype.HasIndex<EntityPrototype>(protoId))
            return false;

        foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(protoId))
        {
            foreach (var candidate in departments)
            {
                if (parent.ID == candidate.Id)
                {
                    department = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private void OnGetProgress(Entity<HooliganBombConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<NumberObjectiveComponent>(ent.Owner, out var number) || number.Target == 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = MathF.Min(ent.Comp.Count / (float) number.Target, 1f);
    }
}
