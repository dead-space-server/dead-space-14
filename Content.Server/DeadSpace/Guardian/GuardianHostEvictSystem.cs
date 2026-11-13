using Content.Server.Guardian;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Shared.DeadSpace.Guardian;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Guardian;

public sealed partial class GuardianHostEvictSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GuardianHostComponent, ComponentStartup>(OnGuardianHostStartup);
        SubscribeLocalEvent<GuardianHostComponent, ComponentRemove>(OnGuardianHostRemove);

        SubscribeLocalEvent<GuardianHostEvictComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GuardianHostEvictComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<GuardianHostEvictComponent, GuardianEvictActionEvent>(OnEvictAction);
    }

    private void OnGuardianHostStartup(Entity<GuardianHostComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<GuardianHostEvictComponent>(ent.Owner);
    }

    private void OnGuardianHostRemove(Entity<GuardianHostComponent> ent, ref ComponentRemove args)
    {
        RemCompDeferred<GuardianHostEvictComponent>(ent.Owner);
    }

    private void OnStartup(Entity<GuardianHostEvictComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<GuardianHostEvictComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
    }

    private void OnEvictAction(Entity<GuardianHostEvictComponent> ent, ref GuardianEvictActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryEvictGuardianPlayer(ent.AsNullable()))
            args.Handled = true;
    }

    public bool TryEvictGuardianPlayer(Entity<GuardianHostEvictComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanEvictGuardianPlayer(ent.Owner, out var guardian))
            return false;

        if (!_mind.TryGetMind(guardian, out var mindId, out var mind))
        {
            _popup.PopupEntity(Loc.GetString("guardian-evict-empty"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return false;
        }

        if (!_ghost.OnGhostAttempt(mindId, true, viaCommand: true, forced: true, mind: mind))
        {
            _popup.PopupEntity(Loc.GetString("guardian-evict-failed"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("guardian-evict-success"), ent.Owner, ent.Owner, PopupType.MediumCaution);
        return true;
    }

    public bool CanEvictGuardianPlayer(EntityUid hostUid, out EntityUid guardian)
    {
        guardian = default;

        if (!TryComp<GuardianHostComponent>(hostUid, out var host) || host.HostedGuardian is null)
            return false;

        guardian = host.HostedGuardian.Value;
        return Exists(guardian);
    }
}
