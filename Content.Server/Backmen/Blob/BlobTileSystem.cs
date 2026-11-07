using System.Linq;
using System.Numerics;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;

namespace Content.Server.Backmen.Blob;

public sealed class BlobTileSystem : SharedBlobTileSystem
{
    private static readonly SoundPathSpecifier BlastSound = new("/Audio/Effects/explosion1.ogg");
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private EntityQuery<BlobCoreComponent> _blobCoreQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobTileComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BlobTileComponent, BlobTileGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobTileComponent, FlashAttemptEvent>(OnFlashAttempt);
        SubscribeLocalEvent<BlobTileComponent, EntityTerminatingEvent>(OnTerminate);
        SubscribeLocalEvent<BlobTileComponent, ReactionEntityEvent>(OnReaction);

        _blobCoreQuery = GetEntityQuery<BlobCoreComponent>();
    }

    private void OnReaction(EntityUid uid, BlobTileComponent component, ref ReactionEntityEvent args)
    {
        if (component.Core == null ||
            !_blobCoreQuery.TryComp(component.Core.Value, out var blobCoreComponent) ||
            blobCoreComponent.CurrentChem != BlobChemType.BlazingOil)
            return;

        if (args.Method != ReactionMethod.Touch || args.Reagent.ID != "Water")
            return;

        var amount = Math.Min(args.ReagentQuantity.Quantity.Float(), 50f) * 0.5f;
        _damageableSystem.TryChangeDamage(uid, new DamageSpecifier
        {
            DamageDict =
            {
                ["Heat"] = FixedPoint2.New(amount),
            }
        });
    }

    private void OnTerminate(EntityUid uid, BlobTileComponent component, EntityTerminatingEvent args)
    {
        if (component.Core == null || TerminatingOrDeleted(component.Core.Value) ||
            !_blobCoreQuery.TryComp(component.Core.Value, out var blobCoreComponent))
            return;
        blobCoreComponent.BlobTiles.Remove(uid);
    }

    private void OnFlashAttempt(EntityUid uid, BlobTileComponent component, FlashAttemptEvent args)
    {
        if (args.Used == null || MetaData(args.Used.Value).EntityPrototype?.ID != "GrenadeFlashBang")
            return;
        if (component.BlobTileType == BlobTileType.Normal)
            _damageableSystem.TryChangeDamage(uid, component.FlashDamage);
    }

    private void OnDestruction(EntityUid uid, BlobTileComponent component, DestructionEventArgs args)
    {
        if (component.Core == null || !_blobCoreQuery.TryComp(component.Core.Value, out var blobCoreComponent))
            return;

        switch (blobCoreComponent.CurrentChem)
        {
            case BlobChemType.ElectromagneticWeb:
            {
                _empSystem.EmpPulse(_transform.GetMapCoordinates(uid), 5f, 50000f, TimeSpan.FromSeconds(3));

                var empDamage = new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Heat"] = FixedPoint2.New(20),
                    }
                };
                foreach (var ent in _lookup.GetEntitiesInRange<DamageableComponent>(_transform.GetMapCoordinates(uid), 5f))
                {
                    if (!HasComp<MobStateComponent>(ent))
                        continue;
                    if (IsBlobFriendly(ent))
                        continue;
                    _damageableSystem.TryChangeDamage(ent.Owner, empDamage);
                }

                break;
            }
            case BlobChemType.ExplosiveLattice:
                BlobDirectionalBlast(uid, component.Core.Value, blobCoreComponent, maxDamage: 30f, reach: 3);
                break;
        }
    }

    private void BlobDirectionalBlast(EntityUid tile, EntityUid coreUid, BlobCoreComponent core, float maxDamage, int reach)
    {
        var xform = Transform(tile);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tileRef = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        var horizontal = core.ExplosionDirection == BlobExplosionDirection.Horizontal;

        for (var offset = -reach; offset <= reach; offset++)
        {
            var indices = horizontal
                ? new Vector2i(tileRef.GridIndices.X + offset, tileRef.GridIndices.Y)
                : new Vector2i(tileRef.GridIndices.X, tileRef.GridIndices.Y + offset);

            var damage = maxDamage * (1f - Math.Abs(offset) / (float) (reach + 1));
            var blastDamage = new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = FixedPoint2.New(damage),
                    ["Structural"] = FixedPoint2.New(damage),
                }
            };

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, indices))
            {
                if (HasComp<BlobTileComponent>(ent))
                    continue;
                if (!HasComp<DestructibleComponent>(ent) || !HasComp<DamageableComponent>(ent))
                    continue;
                _damageableSystem.TryChangeDamage(ent, blastDamage);
            }

            var coords = _mapSystem.ToCenterCoordinates(xform.GridUid.Value, indices, grid);
            _explosion.QueueExplosion(_transform.ToMapCoordinates(coords), core.BlobExplosive, 2f, 2f, 6f, coreUid, maxTileBreak: 0);
            foreach (var ent in _lookup.GetEntitiesInRange<DamageableComponent>(coords, 0.71f))
            {
                if (!HasComp<MobStateComponent>(ent))
                    continue;
                if (IsBlobFriendly(ent))
                    continue;
                _damageableSystem.TryChangeDamage(ent.Owner, blastDamage);
            }
        }

        if (!BlobSilence.BeforeAnnouncement(EntityManager))
            _audioSystem.PlayPvs(BlastSound, tile, AudioParams.Default.WithVolume(3f));
    }

    private bool IsBlobFriendly(EntityUid uid)
    {
        if (HasComp<BlobMobComponent>(uid) ||
            HasComp<ZombieBlobComponent>(uid) ||
            HasComp<BlobCarrierComponent>(uid))
        {
            return true;
        }

        return HasComp<BlobTileComponent>(uid);
    }

    protected override void TryRemove(EntityUid target,
        EntityUid coreUid,
        BlobTileComponent tile,
        BlobCoreComponent core)
    {
        var xform = Transform(target);
        if (!_blobCoreSystem.RemoveBlobTile(target, coreUid, core))
            return;

        FixedPoint2 returnCost = 0;

        if (tile.ReturnCost)
        {
            switch (tile.BlobTileType)
            {
                case BlobTileType.Normal:
                {
                    returnCost = core.NormalBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
                case BlobTileType.Strong:
                {
                    returnCost = core.StrongBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
                case BlobTileType.Factory:
                {
                    returnCost = core.FactoryBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
                case BlobTileType.Resource:
                {
                    returnCost = core.ResourceBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
                case BlobTileType.Reflective:
                {
                    returnCost = core.ReflectiveBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
                case BlobTileType.Node:
                {
                    returnCost = core.NodeBlobCost * core.ReturnResourceOnRemove;
                    break;
                }
            }
        }

        if (returnCost > 0)
        {
            if (_blobCoreQuery.TryComp(tile.Core, out var blobCoreComponent) && blobCoreComponent.Observer != null)
            {
                _popup.PopupCoordinates(Loc.GetString("blob-get-resource", ("point", returnCost)),
                    xform.Coordinates,
                    blobCoreComponent.Observer.Value,
                    PopupType.Large);
            }

            _blobCoreSystem.ChangeBlobPoint(coreUid, returnCost, core);
        }
    }

    private void OnPulsed(EntityUid uid, BlobTileComponent component, BlobTileGetPulseEvent args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null ||
            !_blobCoreQuery.TryComp(blobTileComponent.Core.Value, out var blobCoreComponent))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.RegenerativeMateria)
        {
            var healCore = new DamageSpecifier();
            foreach (var keyValuePair in component.HealthOfPulse.DamageDict)
            {
                healCore.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * 2.5f);
            }

            _damageableSystem.TryChangeDamage(uid, healCore);
        }
        else
            _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);

        if (!args.Handled)
            return;

        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var mobTile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

        var mobAdjacentTiles = new[]
        {
            mobTile.GridIndices.Offset(Direction.East),
            mobTile.GridIndices.Offset(Direction.West),
            mobTile.GridIndices.Offset(Direction.North),
            mobTile.GridIndices.Offset(Direction.South),
        };

        var localPos = xform.Coordinates.Position;

        var radius = 1.0f;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(xform.GridUid.Value,
                grid,
                new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)))
            .ToArray();

        foreach (var innerTile in innerTiles)
        {
            if (!mobAdjacentTiles.Contains(innerTile.GridIndices))
                continue;

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<DestructibleComponent>(ent) || !HasComp<ConstructionComponent>(ent))
                    continue;
                _damageableSystem.TryChangeDamage(ent, blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem]);
                if (!BlobSilence.BeforeAnnouncement(EntityManager))
                    _audioSystem.PlayPvs(blobCoreComponent.AttackSound, uid, AudioParams.Default);
                args.Handled = true;
                return;
            }

            var spawn = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<BlobTileComponent>(ent))
                    continue;
                spawn = false;
                break;
            }

            if (!spawn)
                continue;

            var location = _mapSystem.ToCoordinates(xform.GridUid.Value, innerTile.GridIndices, grid);

            if (_blobCoreSystem.TransformBlobTile(null,
                    blobTileComponent.Core.Value,
                    blobCoreComponent.NormalBlobTile,
                    location,
                    blobCoreComponent,
                    false))
                return;
        }
    }

    protected override void TryUpgrade(EntityUid target,
        EntityUid user,
        EntityUid coreUid,
        BlobTileComponent tile,
        BlobCoreComponent core)
    {
        var xform = Transform(target);
        if (tile.BlobTileType == BlobTileType.Normal)
        {
            if (!_blobCoreSystem.TryUseAbility(user, coreUid, core, core.StrongBlobCost))
                return;

            _blobCoreSystem.TransformBlobTile(target,
                coreUid,
                core.StrongBlobTile,
                xform.Coordinates,
                core,
                transformCost: core.StrongBlobCost);
        }
        else if (tile.BlobTileType == BlobTileType.Strong)
        {
            if (!_blobCoreSystem.TryUseAbility(user, coreUid, core, core.ReflectiveBlobCost))
                return;

            _blobCoreSystem.TransformBlobTile(target,
                coreUid,
                core.ReflectiveBlobTile,
                xform.Coordinates,
                core,
                transformCost: core.ReflectiveBlobCost);
        }
    }
}
