using Content.Server.Administration.Logs;
using Content.Server.Station.Systems;
using Content.Shared.Silicons.AiBorgControl;
using Content.Shared.Actions;
using Content.Shared.Database;
using Content.Shared.Gibbing;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Silicons.Borgs;
using Content.Server.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Popups;
using Content.Shared.Emag.Systems;
using Content.Shared.Silicons.Laws;

namespace Content.Server.Silicons.AiBorgControl;

public sealed class AiBorgControlSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly BorgSystem _borg = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EmagSystem _emag = default!;

    private static readonly EntProtoId ReturnAction = "ActionAiReturnFromBorg";
    private static readonly TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<StationAiHeldComponent>(AiBorgControlUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<AiBorgControlTakeMessage>(OnTake);
        });

        SubscribeLocalEvent<AiDeployedInBorgComponent, MobStateChangedEvent>(OnAiMobState);

        SubscribeLocalEvent<AiControlledBorgComponent, AiBorgReturnToCoreEvent>(OnReturnAction);
        SubscribeLocalEvent<AiControlledBorgComponent, MobStateChangedEvent>(OnBorgMobState);
        SubscribeLocalEvent<AiControlledBorgComponent, MindRemovedMessage>(OnBorgMindRemoved);
        SubscribeLocalEvent<AiControlledBorgComponent, EntityTerminatingEvent>(OnBorgTerminating);
        SubscribeLocalEvent<AiControlledBorgComponent, GetSiliconLawsEvent>(OnGetLaws, before: [typeof(SiliconLawSystem)]);

        var before = new[] { typeof(SharedBorgSystem) };
        SubscribeLocalEvent<AiControlledBorgComponent, EntRemovedFromContainerMessage>(OnContainerRemoved, before: before);
        SubscribeLocalEvent<AiControlledBorgComponent, GibbedBeforeDeletionEvent>(OnGibbed, before: before);
        SubscribeLocalEvent<AiControlledBorgComponent, PowerCellSlotEmptyEvent>(OnPowerEmpty);
        SubscribeLocalEvent<AiControlledBorgComponent, SiliconEmaggedEvent>(OnBorgEmagged);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + UpdateDelay;

        var query = EntityQueryEnumerator<StationAiHeldComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var held, out _))
        {
            if (_ui.IsUiOpen(uid, AiBorgControlUiKey.Key))
                UpdateUi((uid, held));
        }
    }

    private void OnUiOpened(Entity<StationAiHeldComponent> ai, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ai);
    }

    private void UpdateUi(Entity<StationAiHeldComponent> ai)
    {
        var list = new List<AiBorgControlEntry>();
        var query = EntityQueryEnumerator<BorgChassisComponent>();
        while (query.MoveNext(out var borg, out var chassis))
        {
            if (!HasRemoteModule(chassis) || !SameStation(ai, borg))
                continue;

            AiBorgStatus status;
            if (_mobState.IsIncapacitated(borg))
                status = AiBorgStatus.Dead;
            else if (_emag.CheckFlag(borg, EmagType.Interaction))
                status = AiBorgStatus.Emagged;
            else if (HasComp<AiControlledBorgComponent>(borg) || _mind.TryGetMind(borg, out _, out _))
                status = AiBorgStatus.Controlled;
            else if (!_powerCell.HasDrawCharge(borg))
                status = AiBorgStatus.NoPower;
            else
                status = AiBorgStatus.Ready;

            var chassisName = TryComp<BorgTransponderComponent>(borg, out var tr)
                ? tr.Name
                : MetaData(borg).EntityPrototype?.Name ?? string.Empty;

            list.Add(new AiBorgControlEntry(
                GetNetEntity(borg), Name(borg), chassisName,
                _borg.GetBorgHp(borg), _borg.GetBorgCharge(borg), chassis.ModuleCount, status));
        }

        _ui.SetUiState(ai.Owner, AiBorgControlUiKey.Key, new AiBorgControlState(list));
    }

    private bool HasRemoteModule(BorgChassisComponent chassis)
    {
        return chassis.BrainEntity is { } brain && HasComp<BorgRemoteModuleComponent>(brain);
    }

    private bool SameStation(EntityUid ai, EntityUid borg)
    {
        var a = _station.GetOwningStation(ai);
        return a != null && a == _station.GetOwningStation(borg);
    }

    private void OnTake(Entity<StationAiHeldComponent> ai, ref AiBorgControlTakeMessage msg)
    {
        var borg = GetEntity(msg.Borg);

        if (!TryComp<BorgChassisComponent>(borg, out var chassis)
            || !HasRemoteModule(chassis)
            || HasComp<AiDeployedInBorgComponent>(ai)
            || HasComp<AiControlledBorgComponent>(borg)
            || _mobState.IsIncapacitated(ai)
            || _mobState.IsIncapacitated(borg)
            || _mind.TryGetMind(borg, out _, out _)
            || !_powerCell.HasDrawCharge(borg)
            || !SameStation(ai, borg)
            || _emag.CheckFlag(borg, EmagType.Interaction)
            || !_mind.TryGetMind(ai, out var mindId, out var mind))
            return;

        var controlled = EnsureComp<AiControlledBorgComponent>(borg);
        controlled.Ai = ai;
        EnsureComp<AiDeployedInBorgComponent>(ai).Borg = borg;
        _actions.AddAction(borg, ref controlled.ReturnActionEntity, ReturnAction);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ai):ai} took control of borg {ToPrettyString(borg):borg}");

        _ui.CloseUi(ai.Owner, AiBorgControlUiKey.Key, msg.Actor);

        _mind.TransferTo(mindId, borg, mind: mind);
    }


    private void ReturnToCore(Entity<AiControlledBorgComponent> borg)
    {
        var ai = borg.Comp.Ai;
        if (Exists(ai) && !TerminatingOrDeleted(ai)
            && _mind.TryGetMind(borg.Owner, out var mindId, out var mind))
        {
            _mind.TransferTo(mindId, ai, mind: mind);
        }

        Cleanup(borg);
    }

    private void Cleanup(Entity<AiControlledBorgComponent> borg)
    {
        if (borg.Comp.ReturnActionEntity is { } action)
        {
            _actions.RemoveAction(action);
            borg.Comp.ReturnActionEntity = null;
        }

        if (Exists(borg.Comp.Ai) && !TerminatingOrDeleted(borg.Comp.Ai))
            RemCompDeferred<AiDeployedInBorgComponent>(borg.Comp.Ai);

        if (!TerminatingOrDeleted(borg))
            RemCompDeferred<AiControlledBorgComponent>(borg);
    }

    private void OnReturnAction(Entity<AiControlledBorgComponent> ent, ref AiBorgReturnToCoreEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        ReturnToCore(ent);
    }

    private void OnBorgMobState(Entity<AiControlledBorgComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            ReturnToCore(ent);
    }

    private void OnAiMobState(Entity<AiDeployedInBorgComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead
            && TryComp<AiControlledBorgComponent>(ent.Comp.Borg, out var controlled))
            ReturnToCore((ent.Comp.Borg, controlled));
    }

    private void OnBorgMindRemoved(Entity<AiControlledBorgComponent> ent, ref MindRemovedMessage args)
    {
        Cleanup(ent);
    }

    private void OnBorgTerminating(Entity<AiControlledBorgComponent> ent, ref EntityTerminatingEvent args)
    {
        ReturnToCore(ent);
    }

    private void OnContainerRemoved(Entity<AiControlledBorgComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<BorgRemoteModuleComponent>(args.Entity))
            ReturnToCore(ent);
    }

    private void OnGibbed(Entity<AiControlledBorgComponent> ent, ref GibbedBeforeDeletionEvent args)
    {
        ReturnToCore(ent);
    }

    private void OnPowerEmpty(Entity<AiControlledBorgComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        var ai = ent.Comp.Ai;
        ReturnToCore(ent);

        if (Exists(ai) && !TerminatingOrDeleted(ai))
            _popup.PopupEntity(Loc.GetString("ai-borg-control-power-lost"), ai, ai, PopupType.LargeCaution);
    }

    private void OnGetLaws(Entity<AiControlledBorgComponent> ent, ref GetSiliconLawsEvent args)
    {
        if (args.Handled || !Exists(ent.Comp.Ai) || TerminatingOrDeleted(ent.Comp.Ai))
            return;

        args.Laws = _laws.GetLaws(ent.Comp.Ai);
        args.Handled = true;
    }

    private void OnBorgEmagged(Entity<AiControlledBorgComponent> ent, ref SiliconEmaggedEvent args)
    {
        var ai = ent.Comp.Ai;
        ReturnToCore(ent);

        if (Exists(ai) && !TerminatingOrDeleted(ai))
            _popup.PopupEntity(Loc.GetString("ai-borg-control-emagged"), ai, ai, PopupType.LargeCaution);
    }
}
