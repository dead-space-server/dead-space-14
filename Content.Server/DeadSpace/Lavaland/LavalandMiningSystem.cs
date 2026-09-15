using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Server.Store.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Content.Shared.Salvage.Fulton;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMiningSystem : EntitySystem
{
    private static readonly HashSet<ProtoId<AccessLevelPrototype>> DefaultMiningAccess = new()
    {
        "Salvage",
        "SeniorSalvage",
    };

    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedStorageSystem _storageSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandOreRedeemerComponent, InteractUsingEvent>(OnRedeemerInteractUsing,
            before: [typeof(MaterialStorageSystem)]);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, DumpEvent>(OnRedeemerDump,
            before: [typeof(MaterialStorageSystem)]);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, ExaminedEvent>(OnRedeemerExamined);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, MaterialEntitiesEjectedEvent>(OnRedeemerMaterialsEjected);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, LatheMaterialsQueuedEvent>(OnRedeemerLatheMaterialsQueued);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, LatheMaterialsRefundedEvent>(OnRedeemerLatheMaterialsRefunded);
        SubscribeLocalEvent<LavalandOreRedeemerComponent, LatheRecipeFinishedEvent>(OnRedeemerLatheRecipeFinished);
        SubscribeLocalEvent<LavalandRedeemedOreComponent, StackSplitEvent>(OnRedeemedOreStackSplit);
        SubscribeLocalEvent<LavalandRedeemedOreComponent, StackMergedEvent>(OnRedeemedOreStackMerged);

        SubscribeLocalEvent<LavalandMiningPointsComponent, ExaminedEvent>(OnMiningCardExamined);
        SubscribeLocalEvent<LavalandMiningVoucherComponent, ExaminedEvent>(OnMiningVoucherExamined);

        SubscribeLocalEvent<LavalandMiningShopComponent, BeforeActivatableUIOpenEvent>(OnShopBeforeOpen,
            before: [typeof(StoreSystem)]);
        SubscribeLocalEvent<LavalandMiningShopComponent, StoreRequestUpdateInterfaceMessage>(OnShopRequestUpdate,
            before: [typeof(StoreSystem)]);
        SubscribeLocalEvent<LavalandMiningShopComponent, StoreBuyListingMessage>(OnShopBuyBefore,
            before: [typeof(StoreSystem)]);
        SubscribeLocalEvent<LavalandMiningShopComponent, ContainerModifiedMessage>(OnShopSlotChanged);
        SubscribeLocalEvent<LavalandMiningShopComponent, BoundUIOpenedEvent>(OnShopUiOpened);
        SubscribeLocalEvent<LavalandMiningShopComponent, GotEmaggedEvent>(OnShopEmagged);
        SubscribeLocalEvent<LavalandMiningShopComponent, LavalandMiningVoucherRequestUpdateMessage>(OnShopVoucherRequestUpdate);
        SubscribeLocalEvent<LavalandMiningShopComponent, LavalandMiningVoucherRedeemMessage>(OnShopVoucherRedeem);
        SubscribeLocalEvent<LavalandMiningShopComponent, LavalandMiningVoucherEjectMessage>(OnShopVoucherEject);
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnStoreBuyFinished);
    }

    private void OnRedeemerInteractUsing(Entity<LavalandOreRedeemerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<FultonComponent>(args.Used))
            return;

        var usedIsStack = HasComp<StackComponent>(args.Used);
        var usedIsStorage = HasComp<StorageComponent>(args.Used);

        if (!usedIsStack && !usedIsStorage)
        {
            return;
        }

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("lavaland-ore-redeemer-unpowered"), ent.Owner, args.User);
            return;
        }

        var hasCard = TryGetMiningCard(args.User, ent.Comp.MiningAccess, out var cardUid, out var points);

        var result = Redeem(args.User, args.Used, ent.Owner, ent.Comp, hasCard);
        if (result.Units <= 0 && !usedIsStorage)
            return;

        args.Handled = true;
        ApplyRedeemResult(ent.Owner, args.User, result, hasCard, cardUid, points);
    }

    private void OnRedeemerDump(Entity<LavalandOreRedeemerComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-redeemer-unpowered"), ent.Owner, args.User);
            return;
        }

        var hasCard = TryGetMiningCard(args.User, ent.Comp.MiningAccess, out var cardUid, out var points);

        var result = new RedeemResult();
        while (args.DumpQueue.TryDequeue(out var uid))
        {
            if (HasComp<FultonComponent>(uid))
                continue;

            TryRedeemStack(args.User, uid, ent.Owner, ent.Comp, hasCard, ref result);
        }

        args.PlaySound = result.Units > 0;
        ApplyRedeemResult(ent.Owner, args.User, result, hasCard, cardUid, points);
    }

    private void ApplyRedeemResult(
        EntityUid machine,
        EntityUid user,
        RedeemResult result,
        bool hasCard,
        EntityUid cardUid,
        LavalandMiningPointsComponent? points)
    {
        if (result.Units <= 0)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-redeemer-no-ore"), machine, user);
            return;
        }

        if (!hasCard || points == null)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-mining-no-salvage-card"), machine, user);
            return;
        }

        if (result.Points == 0 && result.Debt == 0)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-redeemer-already-credited"), machine, user);
            return;
        }

        var delta = result.Points - result.Debt;
        points.Balance += (result.Debt > 0) ? 0 : delta;
        Dirty(cardUid, points);

        if (result.Debt > 0)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "lavaland-ore-redeemer-redeemed-debt",
                    ("points", result.Points),
                    ("debt", result.Debt),
                    ("balance", points.Balance)),
                machine,
                user);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString(
                "lavaland-ore-redeemer-redeemed",
                ("points", result.Points),
                ("balance", points.Balance)),
            machine,
            user);
    }

    private void OnRedeemerExamined(Entity<LavalandOreRedeemerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryGetMiningCard(args.Examiner, ent.Comp.MiningAccess, out _, out var points))
            args.PushMarkup(Loc.GetString("lavaland-ore-redeemer-examine", ("balance", points.Balance)));
        else
            args.PushMarkup(Loc.GetString("lavaland-ore-redeemer-examine-no-card"));
    }

    private void OnRedeemerLatheRecipeFinished(Entity<LavalandOreRedeemerComponent> ent, ref LatheRecipeFinishedEvent args)
    {
        foreach (var (material, amount) in args.Materials)
        {
            if (amount <= 0)
                continue;

            RemoveCreditedMaterial(ent.Comp.PendingProcessedMaterials, material, amount);
            RemoveCreditedMaterial(ent.Comp.PendingCreditedMaterials, material, amount);
        }
    }

    private void OnRedeemerMaterialsEjected(Entity<LavalandOreRedeemerComponent> ent, ref MaterialEntitiesEjectedEvent args)
    {
        ent.Comp.ProcessedMaterials.TryGetValue(args.Material, out var trackedProcessed);
        // Every ejected unit entered this redeemer, so it must never become fresh ore again.
        var processed = Math.Max(trackedProcessed, args.Amount);
        ent.Comp.CreditedMaterials.TryGetValue(args.Material, out var credited);
        if (processed <= 0 && credited <= 0)
            return;

        foreach (var spawned in args.Entities)
        {
            if (TerminatingOrDeleted(spawned))
                continue;

            var moved = MarkRedeemedMaterial(spawned, args.Material, processed, credited);
            if (moved.Processed <= 0 && moved.Credited <= 0)
                continue;

            processed -= moved.Processed;
            credited -= moved.Credited;
            RemoveCreditedMaterial(ent.Comp.ProcessedMaterials, args.Material, moved.Processed);
            RemoveCreditedMaterial(ent.Comp.CreditedMaterials, args.Material, moved.Credited);

            if (processed <= 0 && credited <= 0)
                break;
        }
    }

    private void OnRedeemerLatheMaterialsQueued(Entity<LavalandOreRedeemerComponent> ent, ref LatheMaterialsQueuedEvent args)
    {
        foreach (var (material, amount) in args.Materials)
        {
            MoveCreditedMaterial(
                ent.Comp.ProcessedMaterials,
                ent.Comp.PendingProcessedMaterials,
                material,
                amount);
            MoveCreditedMaterial(
                ent.Comp.CreditedMaterials,
                ent.Comp.PendingCreditedMaterials,
                material,
                amount);
        }
    }

    private void OnRedeemerLatheMaterialsRefunded(Entity<LavalandOreRedeemerComponent> ent, ref LatheMaterialsRefundedEvent args)
    {
        foreach (var (material, amount) in args.Materials)
        {
            MoveCreditedMaterial(
                ent.Comp.PendingProcessedMaterials,
                ent.Comp.ProcessedMaterials,
                material,
                amount);
            MoveCreditedMaterial(
                ent.Comp.PendingCreditedMaterials,
                ent.Comp.CreditedMaterials,
                material,
                amount);
        }
    }

    private void OnRedeemedOreStackSplit(Entity<LavalandRedeemedOreComponent> ent, ref StackSplitEvent args)
    {
        if (args.Amount <= 0 || ent.Comp.ProcessedUnits <= 0)
            return;

        var movedProcessed = Math.Min(ent.Comp.ProcessedUnits, args.Amount);
        var movedCredited = Math.Min(ent.Comp.CreditedUnits, movedProcessed);

        ent.Comp.ProcessedUnits -= movedProcessed;
        ent.Comp.CreditedUnits -= movedCredited;
        AddRedeemedUnits(args.NewId, movedProcessed, movedCredited, args.Amount);

        if (ent.Comp.ProcessedUnits <= 0)
            RemCompDeferred<LavalandRedeemedOreComponent>(ent.Owner);
    }

    private void OnRedeemedOreStackMerged(Entity<LavalandRedeemedOreComponent> ent, ref StackMergedEvent args)
    {
        if (args.Amount <= 0 || ent.Comp.ProcessedUnits <= 0)
            return;

        var donorCount = TryComp<StackComponent>(ent.Owner, out var donorStack)
            ? donorStack.Count
            : args.Amount;
        var availableProcessed = Math.Min(ent.Comp.ProcessedUnits, donorCount);
        var movedProcessed = Math.Min(availableProcessed, args.Amount);
        var movedCredited = Math.Min(ent.Comp.CreditedUnits, movedProcessed);
        if (movedProcessed <= 0)
            return;

        ent.Comp.ProcessedUnits -= movedProcessed;
        ent.Comp.CreditedUnits -= movedCredited;

        var recipientCount = TryComp<StackComponent>(args.Recipient, out var recipientStack)
            ? recipientStack.Count
            : 0;
        AddRedeemedUnits(args.Recipient, movedProcessed, movedCredited, recipientCount + args.Amount);

        if (ent.Comp.ProcessedUnits <= 0)
            RemCompDeferred<LavalandRedeemedOreComponent>(ent.Owner);
    }

    private void OnMiningCardExamined(Entity<LavalandMiningPointsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("lavaland-mining-card-examine", ("balance", ent.Comp.Balance)));
    }

    private void OnMiningVoucherExamined(Entity<LavalandMiningVoucherComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("lavaland-mining-voucher-examine"));

        foreach (var reward in ent.Comp.Rewards)
        {
            args.PushMarkup(Loc.GetString(
                "lavaland-mining-voucher-examine-reward",
                ("reward", Loc.GetString(reward.Name))));
        }
    }

    private bool TryGetMiningCard(
        EntityUid user,
        HashSet<ProtoId<AccessLevelPrototype>> miningAccess,
        out EntityUid cardUid,
        out LavalandMiningPointsComponent points)
    {
        cardUid = default;
        points = default!;

        if (!_idCard.TryFindIdCard(user, out var idCard) ||
            !HasMiningAccess(idCard.Owner, miningAccess))
        {
            return false;
        }

        var pointsComp = EnsureComp<LavalandMiningPointsComponent>(idCard.Owner);
        points = pointsComp;
        cardUid = idCard.Owner;
        return true;
    }

    private RedeemResult Redeem(
        EntityUid user,
        EntityUid used,
        EntityUid receiver,
        LavalandOreRedeemerComponent component,
        bool awardFreshOre)
    {
        var result = new RedeemResult();

        TryRedeemStack(user, used, receiver, component, awardFreshOre, ref result);

        if (!TryComp<StorageComponent>(used, out var storage))
            return result;

        foreach (var contained in storage.Container.ContainedEntities.ToArray())
        {
            TryRedeemStack(user, contained, receiver, component, awardFreshOre, ref result);
        }

        return result;
    }

    private void TryRedeemStack(
        EntityUid user,
        EntityUid uid,
        EntityUid receiver,
        LavalandOreRedeemerComponent component,
        bool awardFreshOre,
        ref RedeemResult result)
    {
        if (HasComp<FultonComponent>(uid))
            return;

        if (!TryComp<StackComponent>(uid, out var stack) ||
            !TryComp<PhysicalCompositionComponent>(uid, out var composition) ||
            stack.Unlimited ||
            stack.Count <= 0 ||
            !component.OreValues.TryGetValue(stack.StackTypeId, out var pointsPerUnit) ||
            pointsPerUnit <= 0)
        {
            return;
        }

        var count = stack.Count;
        var processedUnits = TryComp<LavalandRedeemedOreComponent>(uid, out var redeemedOre)
            ? Math.Clamp(redeemedOre.ProcessedUnits, 0, count)
            : 0;
        var creditedUnits = redeemedOre != null
            ? Math.Clamp(redeemedOre.CreditedUnits, 0, processedUnits)
            : 0;
        var freshUnits = count - processedUnits;
        var redeemedUnits = count;
        var redeemedFreshUnits = freshUnits;
        var redeemedCreditedUnits = creditedUnits;
        if (HasComp<LatheComponent>(receiver))
        {
            if (!_materialStorage.TryInsertMaterialEntity(user, uid, receiver, showPopup: false))
                return;

            AddProcessedMaterials(component, composition, count);
            AddCreditedMaterials(component, composition, creditedUnits + (awardFreshOre ? freshUnits : 0));
        }
        else if (TryComp<StorageComponent>(receiver, out var storage))
        {
            var beforeCounts = GetStorageStackCounts((receiver, storage), stack.StackTypeId);

            if (!_storageSystem.Insert(receiver, uid, out _, user: user, storageComp: storage, playSound: false))
                return;

            redeemedUnits = GetInsertedStackUnits((receiver, storage), stack.StackTypeId, beforeCounts);
            if (redeemedUnits <= 0)
                return;

            var insertedProcessedUnits = Math.Min(processedUnits, redeemedUnits);
            redeemedCreditedUnits = Math.Min(creditedUnits, insertedProcessedUnits);
            redeemedFreshUnits = redeemedUnits - insertedProcessedUnits;

            AddProcessedMaterials(component, composition, redeemedUnits);
            AddCreditedMaterials(component, composition, redeemedCreditedUnits + (awardFreshOre ? redeemedFreshUnits : 0));
            AddRedeemedFreshUnitsToInsertedStacks(
                (receiver, storage),
                stack.StackTypeId,
                beforeCounts,
                insertedProcessedUnits,
                redeemedFreshUnits,
                awardFreshOre ? redeemedFreshUnits : 0);
        }
        else
        {
            QueueDel(uid);
        }

        result.Units += redeemedUnits;
        result.Points += redeemedFreshUnits * pointsPerUnit;
        result.Debt += redeemedCreditedUnits * pointsPerUnit;
    }

    private Dictionary<EntityUid, int> GetStorageStackCounts(
        Entity<StorageComponent> storage,
        ProtoId<StackPrototype> stackType)
    {
        var counts = new Dictionary<EntityUid, int>();

        foreach (var contained in storage.Comp.Container.ContainedEntities)
        {
            if (!TryComp<StackComponent>(contained, out var containedStack) ||
                containedStack.StackTypeId != stackType)
            {
                continue;
            }

            counts[contained] = containedStack.Count;
        }

        return counts;
    }

    private int GetInsertedStackUnits(
        Entity<StorageComponent> storage,
        ProtoId<StackPrototype> stackType,
        IReadOnlyDictionary<EntityUid, int> beforeCounts)
    {
        var inserted = 0;

        foreach (var contained in storage.Comp.Container.ContainedEntities)
        {
            if (!TryComp<StackComponent>(contained, out var containedStack) ||
                containedStack.StackTypeId != stackType)
            {
                continue;
            }

            inserted += Math.Max(0, containedStack.Count - beforeCounts.GetValueOrDefault(contained));
        }

        return inserted;
    }

    private void AddRedeemedFreshUnitsToInsertedStacks(
        Entity<StorageComponent> storage,
        ProtoId<StackPrototype> stackType,
        IReadOnlyDictionary<EntityUid, int> beforeCounts,
        int insertedProcessedUnits,
        int freshUnits,
        int creditedFreshUnits)
    {
        var processedToSkip = insertedProcessedUnits;
        var freshRemaining = freshUnits;
        var creditedRemaining = creditedFreshUnits;

        foreach (var contained in storage.Comp.Container.ContainedEntities)
        {
            if (freshRemaining <= 0)
                break;

            if (!TryComp<StackComponent>(contained, out var containedStack) ||
                containedStack.StackTypeId != stackType)
            {
                continue;
            }

            var inserted = containedStack.Count - beforeCounts.GetValueOrDefault(contained);
            if (inserted <= 0)
                continue;

            if (processedToSkip > 0)
            {
                var skipped = Math.Min(processedToSkip, inserted);
                processedToSkip -= skipped;
                inserted -= skipped;
            }

            if (inserted <= 0)
                continue;

            var processed = Math.Min(freshRemaining, inserted);
            var credited = Math.Min(creditedRemaining, processed);

            AddRedeemedUnits(contained, processed, credited, containedStack.Count);

            freshRemaining -= processed;
            creditedRemaining -= credited;
        }
    }

    private static void AddProcessedMaterials(
        LavalandOreRedeemerComponent component,
        PhysicalCompositionComponent composition,
        int units)
    {
        AddMaterials(component.ProcessedMaterials, composition, units);
    }

    private static void AddCreditedMaterials(
        LavalandOreRedeemerComponent component,
        PhysicalCompositionComponent composition,
        int units)
    {
        AddMaterials(component.CreditedMaterials, composition, units);
    }

    private static void AddMaterials(
        Dictionary<ProtoId<MaterialPrototype>, int> materials,
        PhysicalCompositionComponent composition,
        int units)
    {
        if (units <= 0)
            return;

        foreach (var (material, amountPerUnit) in composition.MaterialComposition)
        {
            if (amountPerUnit <= 0)
                continue;

            materials[material] = materials.GetValueOrDefault(material) + amountPerUnit * units;
        }
    }

    private static void RemoveCreditedMaterial(
        Dictionary<ProtoId<MaterialPrototype>, int> materials,
        ProtoId<MaterialPrototype> material,
        int amount)
    {
        if (amount <= 0 ||
            !materials.TryGetValue(material, out var credited) ||
            credited <= 0)
        {
            return;
        }

        credited -= Math.Min(credited, amount);
        if (credited <= 0)
            materials.Remove(material);
        else
            materials[material] = credited;
    }

    private static void MoveCreditedMaterial(
        Dictionary<ProtoId<MaterialPrototype>, int> from,
        Dictionary<ProtoId<MaterialPrototype>, int> to,
        ProtoId<MaterialPrototype> material,
        int amount)
    {
        if (amount <= 0 ||
            !from.TryGetValue(material, out var credited) ||
            credited <= 0)
        {
            return;
        }

        var moved = Math.Min(credited, amount);
        RemoveCreditedMaterial(from, material, moved);
        to[material] = to.GetValueOrDefault(material) + moved;
    }

    private (int Processed, int Credited) MarkRedeemedMaterial(
        EntityUid uid,
        ProtoId<MaterialPrototype> material,
        int maxProcessedMaterialAmount,
        int maxCreditedMaterialAmount)
    {
        if (!TryComp<StackComponent>(uid, out var stack) ||
            !TryComp<PhysicalCompositionComponent>(uid, out var composition) ||
            !composition.MaterialComposition.TryGetValue(material, out var amountPerUnit) ||
            amountPerUnit <= 0)
        {
            return default;
        }

        var processedUnits = Math.Min(stack.Count, Math.Max(0, maxProcessedMaterialAmount) / amountPerUnit);
        var creditedUnits = Math.Min(processedUnits, Math.Max(0, maxCreditedMaterialAmount) / amountPerUnit);
        if (processedUnits <= 0)
            return default;

        AddRedeemedUnits(uid, processedUnits, creditedUnits, stack.Count);
        return (processedUnits * amountPerUnit, creditedUnits * amountPerUnit);
    }

    private void AddRedeemedUnits(EntityUid uid, int processedUnits, int creditedUnits, int maxUnits)
    {
        if (processedUnits <= 0 || maxUnits <= 0)
            return;

        var redeemed = EnsureComp<LavalandRedeemedOreComponent>(uid);
        redeemed.ProcessedUnits = Math.Clamp(redeemed.ProcessedUnits + processedUnits, 0, maxUnits);
        redeemed.CreditedUnits = Math.Clamp(redeemed.CreditedUnits + creditedUnits, 0, redeemed.ProcessedUnits);
    }

    private void OnShopBeforeOpen(Entity<LavalandMiningShopComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        SyncShopFromCard(ent, args.User, false);
    }

    private void OnShopRequestUpdate(Entity<LavalandMiningShopComponent> ent,
        ref StoreRequestUpdateInterfaceMessage args)
    {
        SyncShopFromCard(ent, args.Actor, false);
    }

    private void OnShopBuyBefore(Entity<LavalandMiningShopComponent> ent, ref StoreBuyListingMessage args)
    {
        SyncShopFromCard(ent, args.Actor, true);
    }

    private void OnShopSlotChanged(Entity<LavalandMiningShopComponent> ent, ref ContainerModifiedMessage args)
    {
        if (args.Container.ID == LavalandMiningShopComponent.VoucherSlotId)
        {
            if (!TryGetShopVoucher(ent, out _, out _))
                _ui.CloseUi(ent.Owner, LavalandMiningVoucherUiKey.Key);
            else
                UpdateVoucherUi(ent);

            return;
        }

        if (args.Container.ID != LavalandMiningShopComponent.CardSlotId ||
            !TryComp<StoreComponent>(ent.Owner, out var store))
        {
            return;
        }

        if (!SyncShopFromCard(ent, null, false))
            _store.CloseUi(ent.Owner, store);

        _store.UpdateUserInterface(null, ent.Owner, store);
    }

    private void OnShopUiOpened(Entity<LavalandMiningShopComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Equals(args.UiKey, LavalandMiningVoucherUiKey.Key))
            UpdateVoucherUi(ent);
    }

    private void OnShopEmagged(Entity<LavalandMiningShopComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) ||
            _emag.CheckFlag(ent.Owner, EmagType.Interaction) ||
            !TryComp<StoreComponent>(ent.Owner, out var store))
        {
            return;
        }

        store.Categories.Add(ent.Comp.EmagCategory);
        _store.RefreshAllListings(store);
        SyncShopFromCard(ent, null, false);
        _store.UpdateUserInterface(args.UserUid, ent.Owner, store);
        Dirty(ent.Owner, store);

        _popup.PopupEntity(Loc.GetString("lavaland-mining-shop-emagged"), ent.Owner, args.UserUid);
        args.Handled = true;
    }

    private void OnShopVoucherRequestUpdate(Entity<LavalandMiningShopComponent> ent, ref LavalandMiningVoucherRequestUpdateMessage args)
    {
        UpdateVoucherUi(ent);
    }

    private void OnShopVoucherRedeem(Entity<LavalandMiningShopComponent> ent, ref LavalandMiningVoucherRedeemMessage args)
    {
        if (!TryGetShopVoucher(ent, out var voucher, out var voucherComp))
            return;

        if (args.Index < 0 || args.Index >= voucherComp.Rewards.Count)
            return;

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-mining-voucher-vendor-unpowered"), ent.Owner, args.Actor);
            UpdateVoucherUi(ent);
            return;
        }

        var reward = voucherComp.Rewards[args.Index];
        if (!TryRedeemVoucher(ent, args.Actor, voucher, reward))
            return;

        _ui.CloseUi(ent.Owner, LavalandMiningVoucherUiKey.Key);
        UpdateVoucherUi(ent);
    }

    private void OnShopVoucherEject(Entity<LavalandMiningShopComponent> ent, ref LavalandMiningVoucherEjectMessage args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.VoucherSlotId, out var slot))
            return;

        if (_itemSlots.TryEjectToHands(ent.Owner, slot, args.Actor))
            _ui.CloseUi(ent.Owner, LavalandMiningVoucherUiKey.Key, args.Actor);
    }

    private bool TryRedeemVoucher(
        Entity<LavalandMiningShopComponent> shop,
        EntityUid user,
        EntityUid voucher,
        LavalandMiningVoucherReward reward)
    {
        var coords = Transform(shop.Owner).Coordinates;
        var spawned = 0;
        foreach (var entry in reward.Entries)
        {
            if (entry.PrototypeId is not { } prototype ||
                entry.Amount <= 0)
            {
                continue;
            }

            if (!_prototype.HasIndex<EntityPrototype>(prototype))
            {
                _popup.PopupEntity(Loc.GetString("lavaland-mining-voucher-failed"), shop.Owner, user);
                return false;
            }

            for (var i = 0; i < entry.Amount; i++)
            {
                Spawn(prototype, coords);
                spawned++;
            }
        }

        if (spawned <= 0)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-mining-voucher-empty"), shop.Owner, user);
            return false;
        }

        QueueDel(voucher);
        _popup.PopupEntity(
            Loc.GetString("lavaland-mining-voucher-redeemed", ("reward", Loc.GetString(reward.Name))),
            shop.Owner,
            user);

        return true;
    }

    private void UpdateVoucherUi(Entity<LavalandMiningShopComponent> ent)
    {
        TryGetShopVoucher(ent, out _, out var voucher);

        _ui.SetUiState(
            ent.Owner,
            LavalandMiningVoucherUiKey.Key,
            LavalandMiningVoucherUi.CreateState(voucher, this.IsPowered(ent.Owner, EntityManager)));
    }

    private void OnStoreBuyFinished(ref StoreBuyFinishedEvent args)
    {
        if (!TryComp<LavalandMiningShopComponent>(args.StoreUid, out var component))
            return;

        SyncCardFromShop((args.StoreUid, component));
    }

    private bool SyncShopFromCard(Entity<LavalandMiningShopComponent> ent, EntityUid? user, bool popup)
    {
        if (!TryComp<StoreComponent>(ent.Owner, out var store))
            return false;

        if (!TryGetShopCard(ent, out _, out var points))
        {
            SetShopBalance(ent, store, 0);

            if (popup && user is { } userUid)
                _popup.PopupEntity(Loc.GetString("lavaland-mining-shop-no-card"), ent.Owner, userUid);

            return false;
        }

        SetShopBalance(ent, store, points.Balance);
        return true;
    }

    private void SyncCardFromShop(Entity<LavalandMiningShopComponent> ent)
    {
        if (!TryComp<StoreComponent>(ent.Owner, out var store))
            return;

        if (!TryGetShopCard(ent, out var cardUid, out var points))
        {
            SetShopBalance(ent, store, 0);
            return;
        }

        if (points.Balance < 0)
        {
            SetShopBalance(ent, store, 0);
            return;
        }

        var newBalance = store.Balance.TryGetValue(ent.Comp.Currency, out var balance)
            ? Math.Max(0, balance.Int())
            : 0;

        if (points.Balance == newBalance)
            return;

        points.Balance = newBalance;
        Dirty(cardUid, points);
    }

    private bool TryGetShopCard(
        Entity<LavalandMiningShopComponent> ent,
        out EntityUid cardUid,
        out LavalandMiningPointsComponent points)
    {
        cardUid = default;
        points = default!;

        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.CardSlotId, out var slot) ||
            slot.Item is not { } item ||
            !HasMiningAccess(item, ent.Comp.MiningAccess))
        {
            return false;
        }

        var pointsComp = EnsureComp<LavalandMiningPointsComponent>(item);
        points = pointsComp;
        cardUid = item;
        return true;
    }

    private bool HasMiningAccess(EntityUid cardUid, HashSet<ProtoId<AccessLevelPrototype>>? allowedAccess)
    {
        if (!HasComp<IdCardComponent>(cardUid))
            return false;

        var tags = _access.TryGetTags(cardUid);
        if (tags == null)
            return false;

        var allowed = allowedAccess ?? DefaultMiningAccess;
        return tags.Any(allowed.Contains);
    }

    private bool TryGetShopVoucher(
        Entity<LavalandMiningShopComponent> ent,
        out EntityUid voucherUid,
        out LavalandMiningVoucherComponent voucher)
    {
        voucherUid = default;
        voucher = default!;

        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.VoucherSlotId, out var slot) ||
            slot.Item is not { } item ||
            !TryComp(item, out LavalandMiningVoucherComponent? voucherComp))
        {
            return false;
        }

        voucher = voucherComp;
        voucherUid = item;
        return true;
    }

    private void SetShopBalance(Entity<LavalandMiningShopComponent> ent, StoreComponent store, int balance)
    {
        var newBalance = FixedPoint2.New(Math.Max(0, balance));
        if (store.Balance.TryGetValue(ent.Comp.Currency, out var oldBalance) && oldBalance == newBalance)
            return;

        store.Balance[ent.Comp.Currency] = newBalance;
        Dirty(ent.Owner, store);
    }

    private struct RedeemResult
    {
        public int Units;
        public int Points;
        public int Debt;
    }
}
