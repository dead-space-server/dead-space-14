// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT


using Content.Shared.Objectives.Components;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Warps;
using Content.Server.Station.Systems;
using System;

namespace Content.Server.DeadSpace.Hooligan.Objectives;

/// <summary>
/// Логика цели на вандализм 
/// Выбирает место, считает граффити рядом с ним, отвечает о прогрессе
/// </summary>
public sealed class HooliganVandalismConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        
        base.Initialize();

        SubscribeLocalEvent<HooliganVandalismConditionComponent, ObjectiveAfterAssignEvent>(OnAssign);
        SubscribeLocalEvent<HooliganGraffitiDrawnEvent>(OnGraffitiDrawn);
        SubscribeLocalEvent<HooliganVandalismConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

    }

    private void OnAssign(Entity<HooliganVandalismConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (args.Mind.OwnedEntity is not {} hooligan)
            return;
        var station = _station.GetOwningStation(hooligan);

        var warps = new List<EntityUid>();
        var highSecurityWarps = new List<EntityUid>();
        var query = EntityQueryEnumerator<WarpPointComponent>();
        while (query.MoveNext(out var uid, out var warp))
        {
            if (string.IsNullOrWhiteSpace(warp.Location))
                continue;
            if (_station.GetOwningStation(uid) != station)
                continue;
            warps.Add(uid);
            if (IsHighSecurity(uid, ent.Comp))
                highSecurityWarps.Add(uid);
        }

        if (warps.Count == 0)
            return;

        var target = highSecurityWarps.Count > 0 && _random.Prob(ent.Comp.HighSecurityChance)
            ? _random.Pick(highSecurityWarps)
            : _random.Pick(warps);
        ent.Comp.TargetLocation = target;

        var location = Comp<WarpPointComponent>(target).Location;
        var title = Loc.GetString("objective-condition-hooligan-vandalism-title", ("location", location!));
        _metaData.SetEntityName(ent.Owner, title, args.Meta);


    }

    private bool IsHighSecurity(EntityUid warp, HooliganVandalismConditionComponent comp)
    {
        var protoId = MetaData(warp).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        foreach (var candidate in comp.HighSecurityBeacons)
        {
            if (candidate.Id == protoId)
                return true;
        }

        if (!_prototype.HasIndex<EntityPrototype>(protoId))
            return false;

        foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(protoId))
        {
            foreach (var candidate in comp.HighSecurityBeacons)
            {
                if (parent.ID == candidate.Id)
                    return true;
            }
        }

        return false;
    }

    private void OnGraffitiDrawn(ref HooliganGraffitiDrawnEvent args)
    {
        if (!_mind.TryGetObjectiveComp<HooliganVandalismConditionComponent>(args.Drawer, out var condition))
            return;

        if (condition.TargetLocation is not {} target)
            return;
        
        var drawerCoords = Transform(args.Drawer).Coordinates;
        var targetCoords = Transform(target).Coordinates;
        if (!_transform.InRange(drawerCoords, targetCoords, condition.Range))
            return;
        
        condition.GraffitiCount++;
        
    }

    private void OnGetProgress(Entity<HooliganVandalismConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<NumberObjectiveComponent>(ent.Owner, out var number) || number.Target == 0)
        {
            args.Progress = 1f;
            return;
        }
        args.Progress = MathF.Min(ent.Comp.GraffitiCount / (float) number.Target, 1f);
    }

}
