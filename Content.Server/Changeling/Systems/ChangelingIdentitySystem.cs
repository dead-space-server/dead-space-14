using System.Linq;
using Content.Server.Antag.Components;
using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Cloning.Events;
using Content.Shared.Store.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Changeling.Systems;

public sealed partial class ChangelingIdentitySystem : SharedChangelingIdentitySystem
{
    // DS14-start
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private static readonly EntProtoId ChangelingRule = "Changeling";
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingIdentityComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<ChangelingIdentityComponent, CloningEvent>(OnClone); // DS14
    }

    // DS14-start
    private void OnClone(Entity<ChangelingIdentityComponent> ent, ref CloningEvent args)
    {
        if (!args.Settings.EventComponents.Contains(Factory.GetRegistration<ChangelingIdentityComponent>().Name) ||
            !_prototype.Index(ChangelingRule).TryGetComponent<AntagSelectionComponent>(out var selection, Factory) ||
            selection.Definitions.Count == 0)
        {
            return;
        }

        var clone = args.CloneUid;
        EntityManager.AddComponents(clone, selection.Definitions[0].Components);

        var identity = Comp<ChangelingIdentityComponent>(clone);
        foreach (var initial in identity.ConsumedIdentities.ToArray())
        {
            if (initial.Identity is { } stored)
                DropStoredIdentity((clone, identity), stored);
        }

        identity.ConsumedIdentities.Clear();
        identity.CurrentIdentity = null;
        identity.IdentityCloningSettings = ent.Comp.IdentityCloningSettings;
        identity.MaxStoredDisguises = ent.Comp.MaxStoredDisguises;

        // Stored bodies must be independent: deleting the old changeling also deletes its identity backups.
        foreach (var source in ent.Comp.ConsumedIdentities)
        {
            var data = _serialization.CreateCopy(source, notNullableOverride: true);
            data.Identity = null;
            if (source.Identity is { } sourceIdentity && Exists(sourceIdentity))
            {
                data.Identity = CloneToPausedMap(identity.IdentityCloningSettings, sourceIdentity);
                if (data.Identity is { } stored &&
                    TryComp<ChangelingStoredIdentityComponent>(sourceIdentity, out var sourceStored))
                {
                    var targetStored = Comp<ChangelingStoredIdentityComponent>(stored);
                    targetStored.OriginalEntity = sourceStored.OriginalEntity;
                    targetStored.OriginalSession = sourceStored.OriginalSession;
                }
            }

            if (source.Identity != null && source.Identity == ent.Comp.CurrentIdentity)
                identity.CurrentIdentity = data.Identity;

            identity.ConsumedIdentities.Add(data);
            if (data.Original is { } original && Exists(original))
                AddDevouredReference((clone, identity), original);
        }

        Dirty(clone, identity);

        if (!TryComp<StoreComponent>(ent, out var sourceStore) || !TryComp<StoreComponent>(clone, out var store))
            return;

        store.Balance = new(sourceStore.Balance);
        store.BalanceSpent = new(sourceStore.BalanceSpent);
        store.AccountOwner = sourceStore.AccountOwner;
        store.FullListingsCatalog = _serialization.CreateCopy(sourceStore.FullListingsCatalog, notNullableOverride: true);
        store.BoughtEntities.Clear();

        foreach (var listing in store.FullListingsCatalog)
        {
            if (listing.PurchaseAmount <= 0)
                continue;

            if (listing.ProductComponents is { } product && _prototype.TryIndex(product, out var components))
                EntityManager.AddComponents(clone, components.Components);

            // Mind-owned actions follow the original mind; only body-owned purchases need new actions.
            if (!listing.ApplyToMob || listing.ProductAction is not { } action)
                continue;

            for (var i = 0; i < listing.PurchaseAmount; i++)
            {
                if (_actions.AddAction(clone, action) is { } actionEntity)
                    store.BoughtEntities.Add(actionEntity);
            }
        }

        Dirty(clone, store);
    }
    // DS14-end

    private void OnGetState(Entity<ChangelingIdentityComponent> entity, ref ComponentGetState args)
    {
        List<ChangelingNetworkedIdentityData> sentIdentities = new();

        foreach (var identity in entity.Comp.ConsumedIdentities)
        {
            ChangelingNetworkedIdentityData netData = new()
            {
                Identity = GetNetEntity(identity.Identity),
                Original = GetNetEntity(identity.Original),
                OriginalJob = identity.OriginalJob,
                OriginalName = identity.OriginalName,
                Starting = identity.Starting,
                GrantedDna = identity.GrantedDna,
            };

            sentIdentities.Add(netData);
        }

        var current = entity.Comp.CurrentIdentity;

        var netCurrent = GetNetEntity(current);

        args.State = new ChangelingIdentityComponentState(
            sentIdentities,
            netCurrent,
            entity.Comp.IdentityCloningSettings,
            entity.Comp.MaxStoredDisguises);
    }
}
