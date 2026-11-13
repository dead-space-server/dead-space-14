using System.Linq;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Lavaland;

public sealed partial class SharedLavalandMiningShopSystem : EntitySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandMiningShopComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<LavalandMiningShopComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<LavalandMiningShopComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LavalandMiningShopComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnComponentInit(EntityUid uid, LavalandMiningShopComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, LavalandMiningShopComponent.CardSlotId, component.CardSlot);
        _itemSlots.AddItemSlot(uid, LavalandMiningShopComponent.VoucherSlotId, component.VoucherSlot);
    }

    private void OnComponentRemove(EntityUid uid, LavalandMiningShopComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CardSlot);
        _itemSlots.RemoveItemSlot(uid, component.VoucherSlot);
    }

    private void OnInteractUsing(Entity<LavalandMiningShopComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<LavalandMiningVoucherComponent>(args.Used))
        {
            args.Handled = true;

            if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.VoucherSlotId, out var voucherSlot) ||
                !_itemSlots.CanInsert(ent.Owner, args.Used, args.User, voucherSlot, voucherSlot.Swap) ||
                !_hands.TryDrop(args.User, args.Used) ||
                !_itemSlots.TryInsert(ent.Owner, voucherSlot, args.Used, args.User, excludeUserAudio: true))
            {
                return;
            }

            if (voucherSlot.InsertSuccessPopup.HasValue)
                _popup.PopupClient(Loc.GetString(voucherSlot.InsertSuccessPopup.Value), ent.Owner, args.User);

            SetVoucherUiState(ent, args.Used);

            if (_timing.IsFirstTimePredicted)
                _ui.TryOpenUi(ent.Owner, LavalandMiningVoucherUiKey.Key, args.User, predicted: true);

            return;
        }

        // Let emag interactions pass through to EmagSystem without the card slot showing a whitelist popup.
        if (HasComp<EmagComponent>(args.Used))
            return;

        if (!TryPrepareMiningCard(args.Used, ent.Comp))
        {
            _popup.PopupClient(Loc.GetString("lavaland-mining-shop-wrong-card"), ent.Owner, args.User);
            return;
        }

        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.CardSlotId, out var slot) ||
            !_itemSlots.CanInsert(ent.Owner, args.Used, args.User, slot, slot.Swap) ||
            !_hands.TryDrop(args.User, args.Used) ||
            !_itemSlots.TryInsert(ent.Owner, slot, args.Used, args.User, excludeUserAudio: true))
        {
            return;
        }

        if (slot.InsertSuccessPopup.HasValue)
            _popup.PopupClient(Loc.GetString(slot.InsertSuccessPopup.Value), ent.Owner, args.User);

        args.Handled = true;
    }

    private void OnOpenAttempt(Entity<LavalandMiningShopComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryGetShopVoucher(ent, out var voucherUid))
        {
            args.Cancel();
            SetVoucherUiState(ent, voucherUid);

            if (_timing.IsFirstTimePredicted)
                _ui.TryOpenUi(ent.Owner, LavalandMiningVoucherUiKey.Key, args.User, predicted: true);

            return;
        }

        if (HasValidCard(ent))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupPredicted(Loc.GetString("lavaland-mining-shop-no-card"), ent, args.User);
    }

    public bool HasValidCard(Entity<LavalandMiningShopComponent> ent)
    {
        return TryGetShopCard(ent, out _);
    }

    public bool HasValidVoucher(Entity<LavalandMiningShopComponent> ent)
    {
        return TryGetShopVoucher(ent, out _);
    }

    public bool TryGetShopCard(Entity<LavalandMiningShopComponent> ent, out EntityUid cardUid)
    {
        cardUid = default;

        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.CardSlotId, out var slot) ||
            slot.Item is not { } item ||
            !TryPrepareMiningCard(item, ent.Comp))
        {
            return false;
        }

        cardUid = item;
        return true;
    }

    public bool TryGetShopVoucher(Entity<LavalandMiningShopComponent> ent, out EntityUid voucherUid)
    {
        voucherUid = default;

        if (!_itemSlots.TryGetSlot(ent.Owner, LavalandMiningShopComponent.VoucherSlotId, out var slot) ||
            slot.Item is not { } item ||
            !HasComp<LavalandMiningVoucherComponent>(item))
        {
            return false;
        }

        voucherUid = item;
        return true;
    }

    private bool TryPrepareMiningCard(EntityUid cardUid, LavalandMiningShopComponent component)
    {
        if (!HasComp<IdCardComponent>(cardUid) || !HasMiningAccess(cardUid, component))
            return false;

        EnsureComp<LavalandMiningPointsComponent>(cardUid);
        return true;
    }

    private bool HasMiningAccess(EntityUid cardUid, LavalandMiningShopComponent component)
    {
        var tags = _access.TryGetTags(cardUid);
        return tags != null && tags.Any(component.MiningAccess.Contains);
    }

    private void SetVoucherUiState(Entity<LavalandMiningShopComponent> ent, EntityUid voucherUid)
    {
        if (!TryComp(voucherUid, out LavalandMiningVoucherComponent? voucher))
            return;

        _ui.SetUiState(
            ent.Owner,
            LavalandMiningVoucherUiKey.Key,
            LavalandMiningVoucherUi.CreateState(voucher, powered: true));
    }
}
