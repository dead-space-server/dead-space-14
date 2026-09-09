// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Actions;
using Content.Server.DeadSpace.ERT.Components;
using Content.Server.EUI;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.GameTicking;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles.Components;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.ERT;

public sealed class ResponseErtImplantSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly ErtResponseSystem _response = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly HashSet<NetUserId> _usedPlayers = new();
    private readonly Dictionary<EntityUid, YesNoEui> _confirmations = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResponseErtImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<ResponseErtImplantComponent, ImplantRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ResponseErtImplantComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ResponseErtImplantComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnMobStateChanged);
        SubscribeLocalEvent<ResponseErtImplantComponent, CallErtHelpActionEvent>(OnCallHelp);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnImplanted(Entity<ResponseErtImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        UpdateAction(ent);
    }

    private void OnRemoved(Entity<ResponseErtImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        CloseConfirmation(ent);
    }

    private void OnShutdown(Entity<ResponseErtImplantComponent> ent, ref ComponentShutdown args)
    {
        CloseConfirmation(ent);
        if (TryComp<SubdermalImplantComponent>(ent, out var implant))
            _actions.SetEnabled(implant.Action, false);
    }

    private void OnMobStateChanged(Entity<ResponseErtImplantComponent> ent, ref ImplantRelayEvent<MobStateChangedEvent> args)
    {
        UpdateAction(ent);
    }

    private void UpdateAction(Entity<ResponseErtImplantComponent> ent)
    {
        if (!TryComp<SubdermalImplantComponent>(ent, out var implant))
            return;

        var enabled = !ent.Comp.Used &&
                      TryComp<MobStateComponent>(implant.ImplantedEntity, out var state) &&
                      ent.Comp.AllowedStates.Contains(state.CurrentState);
        _actions.SetEnabled(implant.Action, enabled);
        if (!enabled)
            CloseConfirmation(ent);
    }

    private void OnCallHelp(Entity<ResponseErtImplantComponent> ent, ref CallErtHelpActionEvent args)
    {
        if (args.Handled || !_players.TryGetSessionByEntity(args.Performer, out var player))
            return;

        args.Handled = true;
        if (!CanCallHelp(ent, args.Performer, player, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        if (_confirmations.ContainsKey(ent))
            return;

        var confirmation = new YesNoEui(ent, args.Performer, this);
        _confirmations.Add(ent, confirmation);
        _eui.OpenEui(confirmation, player);
    }

    public bool TryCallHelp(Entity<ResponseErtImplantComponent> ent, EntityUid target, ICommonSession player, out string reason)
    {
        if (!CanCallHelp(ent, target, player, out reason))
            return false;

        var callReason = Loc.GetString(ent.Comp.CallReason, ("name", Name(target)));
        if (!_response.TryCallErt(ent.Comp.Team,
                _station.GetOwningStation(target),
                out var failure,
                toPay: false,
                needCooldown: false,
                needWarn: false,
                callReason: callReason,
                pinpointerTarget: target,
                requestedByName: player.Name))
        {
            reason = Loc.GetString("ert-critical-force-call-failed", ("reason", failure ?? string.Empty));
            return false;
        }

        ent.Comp.Used = true;
        _usedPlayers.Add(player.UserId);
        UpdateAction(ent);
        reason = Loc.GetString("ert-critical-force-call-accepted");
        return true;
    }

    private bool CanCallHelp(Entity<ResponseErtImplantComponent> ent, EntityUid target, ICommonSession player, out string reason)
    {
        reason = Loc.GetString("ert-critical-force-call-unavailable");
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target) || player.AttachedEntity != target ||
            !TryComp<ResponseErtImplantComponent>(ent, out var response) || response != ent.Comp ||
            !TryComp<SubdermalImplantComponent>(ent, out var implant) || implant.ImplantedEntity != target)
            return false;

        if (ent.Comp.Used || _usedPlayers.Contains(player.UserId))
        {
            reason = Loc.GetString("ert-critical-force-call-used");
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var state) || !ent.Comp.AllowedStates.Contains(state.CurrentState))
            return false;

        var mind = _mind.GetMind(target);
        if (mind == null)
            return false;

        if (!IsRoleAllowed(ent.Comp, mind.Value))
        {
            reason = Loc.GetString(ent.Comp.RoleDeniedMessage);
            return false;
        }

        if (_station.GetOwningStation(target) is not { } station || TerminatingOrDeleted(station))
        {
            reason = Loc.GetString("ert-critical-force-call-off-station");
            return false;
        }

        return true;
    }

    public bool IsRoleAllowed(ResponseErtImplantComponent implant, EntityUid mind)
    {
        if (!TryComp<MindComponent>(mind, out var component))
            return false;

        var matched = implant.RequiredAntagonistRole == null;
        foreach (var role in component.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<MindRoleComponent>(role, out var mindRole) || !mindRole.Antag && !mindRole.ExclusiveAntag)
                continue;

            if (implant.RequiredAntagonistRole is not { } required || MetaData(role).EntityPrototype?.ID != required.Id)
                return false;

            matched = true;
        }

        return matched;
    }

    public void ConfirmationClosed(EntityUid implant, YesNoEui confirmation)
    {
        if (_confirmations.TryGetValue(implant, out var current) && current == confirmation)
            _confirmations.Remove(implant);
    }

    private void CloseConfirmation(EntityUid implant)
    {
        if (_confirmations.Remove(implant, out var confirmation))
            confirmation.Close();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _usedPlayers.Clear();
        foreach (var implant in _confirmations.Keys.ToArray())
            CloseConfirmation(implant);
    }
}
