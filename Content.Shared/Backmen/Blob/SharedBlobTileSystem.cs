using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared.Backmen.Blob;

public abstract class SharedBlobTileSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected EntityQuery<BlobObserverComponent> ObserverQuery;
    protected EntityQuery<BlobCoreComponent> CoreQuery;
    protected EntityQuery<TransformComponent> TransformQuery;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobTileComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<BlobTileComponent, GetVerbsEvent<Verb>>(AddRemoveVerb);

        ObserverQuery = GetEntityQuery<BlobObserverComponent>();
        CoreQuery = GetEntityQuery<BlobCoreComponent>();
        TransformQuery = GetEntityQuery<TransformComponent>();
    }

    private void AddAlternativeVerbs(EntityUid uid, BlobTileComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        AddUpgradeVerb(uid, component, args);
        AddExplosionDirectionVerb(uid, component, args);
    }
    private void AddExplosionDirectionVerb(EntityUid uid, BlobTileComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ObserverQuery.TryGetComponent(args.User, out var ghostBlobComponent))
            return;

        if (ghostBlobComponent.Core == null ||
            !CoreQuery.TryGetComponent(ghostBlobComponent.Core.Value, out var blobCoreComponent))
            return;

        if (ghostBlobComponent.Core.Value != component.Core)
            return;

        if (blobCoreComponent.CurrentChem != BlobChemType.ExplosiveLattice)
            return;

        var verb = new AlternativeVerb
        {
            Act = () =>
            {
                blobCoreComponent.ExplosionDirection = blobCoreComponent.ExplosionDirection == BlobExplosionDirection.Horizontal
                    ? BlobExplosionDirection.Vertical
                    : BlobExplosionDirection.Horizontal;

                _popup.PopupEntity(
                    Loc.GetString("blob-verb-explosion-direction", ("dir", Loc.GetString(blobCoreComponent.ExplosionDirection == BlobExplosionDirection.Horizontal
                        ? "blob-explosion-direction-horizontal"
                        : "blob-explosion-direction-vertical"))),
                    args.User,
                    args.User,
                    PopupType.Medium);
            },
            Text = Loc.GetString("blob-verb-explosion-direction-toggle",
                ("dir", Loc.GetString(blobCoreComponent.ExplosionDirection == BlobExplosionDirection.Horizontal
                    ? "blob-explosion-direction-horizontal"
                    : "blob-explosion-direction-vertical"))),
            Priority = -1,
        };
        args.Verbs.Add(verb);
    }

    protected abstract void TryRemove(EntityUid target, EntityUid coreUid, BlobTileComponent tile, BlobCoreComponent core);

    protected abstract void TryUpgrade(EntityUid target, EntityUid user, EntityUid coreUid, BlobTileComponent tile, BlobCoreComponent core);

    private void AddRemoveVerb(EntityUid uid, BlobTileComponent component, GetVerbsEvent<Verb> args)
    {
        if (!ObserverQuery.TryGetComponent(args.User, out var ghostBlobComponent))
            return;

        if (ghostBlobComponent.Core == null ||
            !CoreQuery.TryGetComponent(ghostBlobComponent.Core.Value, out var blobCoreComponent))
            return;

        if (ghostBlobComponent.Core.Value != component.Core)
            return;

        if (TransformQuery.TryGetComponent(uid, out var transformComponent) && !transformComponent.Anchored)
            return;

        if (CoreQuery.HasComponent(uid))
            return;

        Verb verb = new()
        {
            Act = () => TryRemove(uid, ghostBlobComponent.Core.Value, component, blobCoreComponent),
            Text = Loc.GetString("blob-verb-remove-blob-tile"),
        };
        args.Verbs.Add(verb);
    }

    private void AddUpgradeVerb(EntityUid uid, BlobTileComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ObserverQuery.TryGetComponent(args.User, out var ghostBlobComponent))
            return;

        if (ghostBlobComponent.Core == null ||
            !CoreQuery.TryGetComponent(ghostBlobComponent.Core.Value, out var blobCoreComponent))
            return;

        if (TransformQuery.TryGetComponent(uid, out var transformComponent) && !transformComponent.Anchored)
            return;

        var verbName = component.BlobTileType switch
        {
            BlobTileType.Normal => Loc.GetString("blob-verb-upgrade-to-strong"),
            BlobTileType.Strong => Loc.GetString("blob-verb-upgrade-to-reflective"),
            _ => Loc.GetString("blob-verb-upgrade")
        };

        AlternativeVerb verb = new()
        {
            Act = () => TryUpgrade(uid, args.User, ghostBlobComponent.Core.Value, component, blobCoreComponent),
            Text = verbName
        };
        args.Verbs.Add(verb);
    }
}
