using Content.Server.Actions;
using Content.Server.Backmen.Blob.Components;
using Content.Shared.Gibbing;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.DeadSpace.Administration;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob;

public sealed partial class BlobCarrierSystem : SharedBlobCarrierSystem
{
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private ActionsSystem _action = default!;
    // DS14-start
    [Dependency] private BlobAntagRollbackSystem _rollback = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCarrierComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BlobCarrierComponent, TransformToBlobActionEvent>(OnTransformToBlobChanged);

        SubscribeLocalEvent<BlobCarrierComponent, MapInitEvent>(OnStartup);

        SubscribeLocalEvent<BlobCarrierComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BlobCarrierComponent, MindRemovedMessage>(OnMindRemove);
    }

    private static readonly EntProtoId ActionTransformToBlob = "ActionTransformToBlob";

    private void OnMindAdded(EntityUid uid, BlobCarrierComponent component, MindAddedMessage args)
    {
        component.HasMind = true;
    }

    private void OnMindRemove(EntityUid uid, BlobCarrierComponent component, MindRemovedMessage args)
    {
        component.HasMind = false;
    }

    private void OnTransformToBlobChanged(Entity<BlobCarrierComponent> uid, ref TransformToBlobActionEvent args)
    {
        TransformToBlob(uid);
    }

    private void OnStartup(EntityUid uid, BlobCarrierComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.TransformToBlob, ActionTransformToBlob);

        var ghostRole = EnsureComp<GhostRoleComponent>(uid);
        EnsureComp<GhostTakeoverAvailableComponent>(uid);
        ghostRole.RoleName = Loc.GetString("blob-carrier-role-name");
        ghostRole.RoleDescription = Loc.GetString("blob-carrier-role-desc");
        ghostRole.RoleRules = Loc.GetString("blob-carrier-role-rules");

        EnsureComp<BlobSpeakComponent>(uid).OverrideName = false;
    }

    private void OnMobStateChanged(Entity<BlobCarrierComponent> uid, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            TransformToBlob(uid);
        }
    }

    protected override void TransformToBlob(Entity<BlobCarrierComponent> ent)
    {
        var xform = Transform(ent);
        if (!HasComp<MapGridComponent>(xform.GridUid))
            return;

        if (_mind.TryGetMind(ent, out _, out var mind) && mind.UserId != null)
        {
            // DS14-start
            var userId = mind.UserId.Value;
            _rollback.PreserveBody(userId, ent, _transform.GetMapCoordinates(ent));
            // DS14-end
            var core = Spawn(ent.Comp.CoreBlobPrototype, xform.Coordinates);

            if (!TryComp<BlobCoreComponent>(core, out var blobCoreComponent))
                return;

            _blobCoreSystem.CreateBlobObserver(core, userId, blobCoreComponent); // DS14
            // DS14-start
            _rollback.MovePreservedBodyToNullspace(userId);
            return;
            // DS14-end
        }
        else
        {
            Spawn(ent.Comp.CoreBlobGhostRolePrototype, xform.Coordinates);
        }

        _gibbing.Gib(ent);
    }
}
