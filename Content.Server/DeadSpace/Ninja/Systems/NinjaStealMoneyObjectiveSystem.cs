// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaStealMoneyObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaStealMoneyConditionComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaStealMoneyConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }
    private void OnObjectiveAssigned(Entity<NinjaStealMoneyConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (GetObjectiveStation(args.Mind) is not { } station)
        {
            args.Cancelled = true;
            return;
        }

        if (!TryComp<StationBankAccountComponent>(station, out var accounts))
        {
            args.Cancelled = true;
            return;
        }

        var total = 0;
        foreach (var (account, balance) in accounts.Accounts)
        {
            if (account == "Taipan")
                continue;

            total += balance;
        }

        if (total <= 0)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.TargetCount = (int)Math.Round(total * ent.Comp.PercentageOfMaximumBalance);
        _metaData.SetEntityName(ent, Loc.GetString("objective-condition-ninja-steal-money-title"));
        _metaData.SetEntityDescription(ent, Loc.GetString(
            "ninja-steal-money-objective-description",
            ("count", ent.Comp.TargetCount)));
    }

    private void OnGetProgress(Entity<NinjaStealMoneyConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var comp = ent.Comp;
        if (comp.TargetCount <= 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = Math.Clamp((float) comp.CurrentCount / comp.TargetCount, 0f, 1f);
    }

    private EntityUid? GetObjectiveStation(MindComponent mind)
    {
        if (mind.OwnedEntity is { } owned && !TerminatingOrDeleted(owned))
        {
            if (_station.GetOwningStation(owned) is { } owningStation)
                return owningStation;

            if (TryComp(owned, out TransformComponent? xform) &&
                _station.GetStationInMap(xform.MapID) is { } mapStation)
            {
                return mapStation;
            }
        }

        foreach (var station in _station.GetStations())
        {
            if (HasComp<StationBankAccountComponent>(station))
                return station;
        }

        return null;
    }

    public void AddMoneyCount(NinjaStealMoneyConditionComponent comp, int toAdd)
    {
        comp.CurrentCount = Math.Clamp(comp.CurrentCount + toAdd, 0, comp.TargetCount);
    }
}