// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.DeadSpace.Interfaces.Server;
using Content.Server.DeadSpace.ERT;
using Content.Server.DeadSpace.ERT.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.SponsorLoadout;

public sealed class SponsorEvacuationSystem : EntitySystem
{
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ResponseErtImplantSystem _response = default!;

    private readonly EntProtoId[] _implantPrototypes = ["TrackingImplantCriticalForce", "TrackingImplantSyndicateEvacuation"];
    private readonly HashSet<NetUserId> _recipients = new();
    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
        SubscribeLocalEvent<SponsorEvacuationComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (TerminatingOrDeleted(args.Mob) || args.JobId == null ||
            !HasComp<HumanoidAppearanceComponent>(args.Mob) || !HasComp<MobStateComponent>(args.Mob) ||
            !IoCManager.Instance!.TryResolveType<IServerSponsorsManager>(out var sponsors) ||
            !sponsors.HasEvacuationSupport(args.Player.UserId) ||
            !_recipients.Add(args.Player.UserId))
            return;

        var grant = EnsureComp<SponsorEvacuationComponent>(args.Mob);
        grant.UserId = args.Player.UserId;
        if (grant.Implant == null && TryComp<ImplantedComponent>(args.Mob, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                if (!HasComp<ResponseErtImplantComponent>(implant))
                    continue;

                grant.Implant = implant;
                break;
            }
        }

        _pending.Add(args.Mob);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        QueueUpdate(args.Mind.OwnedEntity);
    }

    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        QueueUpdate(args.Mind.OwnedEntity);
    }

    private void OnMindAdded(EntityUid uid, SponsorEvacuationComponent component, MindAddedMessage args)
    {
        _pending.Add(uid);
    }

    private void QueueUpdate(EntityUid? uid)
    {
        if (uid is { } body && HasComp<SponsorEvacuationComponent>(body))
            _pending.Add(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending.ToArray())
        {
            _pending.Remove(uid);
            if (!TerminatingOrDeleted(uid) && TryComp<SponsorEvacuationComponent>(uid, out var grant))
                UpdateImplant((uid, grant));
        }
    }

    private void UpdateImplant(Entity<SponsorEvacuationComponent> ent)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind) || mind.UserId != ent.Comp.UserId)
            return;

        EntProtoId? desired = null;
        foreach (var prototypeId in _implantPrototypes)
        {
            var prototype = _prototypes.Index(prototypeId);
            if (prototype.TryGetComponent<ResponseErtImplantComponent>(out var response, Factory) &&
                _response.IsRoleAllowed(response, mindId))
            {
                desired = prototypeId;
                break;
            }
        }

        if (ent.Comp.Implant is { } existing)
        {
            if (TerminatingOrDeleted(existing) ||
                !TryComp<SubdermalImplantComponent>(existing, out var implant) || implant.ImplantedEntity != ent.Owner ||
                !TryComp<ResponseErtImplantComponent>(existing, out var response) || response.Used)
                return;

            if (MetaData(existing).EntityPrototype?.ID == desired?.Id)
                return;

            _implants.ForceRemove(ent.Owner, existing);
            ent.Comp.Implant = null;
        }

        if (desired is { } implantId)
            ent.Comp.Implant = _implants.AddImplant(ent, implantId);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _recipients.Clear();
        _pending.Clear();
    }
}
