// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.CartridgeLoader;
using Content.Server.Chat.Managers;
using Content.Server.Backmen.Economy;
using Content.Server.PDA.Ringer;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.CartridgeLoader.Cartridges;
using Content.Shared.Backmen.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chat;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Shared.Popups;
using Content.Shared.Store;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.CartridgeLoader.Cartridges;

public sealed partial class BankCartridgeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private RingerSystem _ringerSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private BankManagerSystem _bankManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankCartridgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BankCartridgeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<BankAccountComponent, ChangeBankAccountBalanceEvent>(OnChangeBankBalance);
        SubscribeLocalEvent<BankAccountComponent, EntGotInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<BankAccountComponent, EntGotRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnUiMessage(EntityUid uid, BankCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not BankTransferMessage message)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        if (!loaderUid.IsValid())
            return;

        if (!TryGetPdaBankAccount(loaderUid, out var sourceAccount) ||
            sourceAccount is not { } source)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-no-account");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState());
            return;
        }

        if (message.Amount <= 0)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-invalid-amount");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        if (string.IsNullOrWhiteSpace(message.SourceAccountPin) ||
            source.Comp.AccountPin != message.SourceAccountPin)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-invalid-pin");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        var targetAccountNumber = message.TargetAccountNumber.Trim();
        if (!_bankManager.TryGetBankAccount(targetAccountNumber, out var targetAccount) ||
            targetAccount is not { } target)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-account-not-found");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        if (source.Owner == target.Owner)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-self");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        if (source.Comp.CurrencyType != target.Comp.CurrencyType)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-currency");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        if (source.Comp.Balance < message.Amount)
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-insufficient");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        if (!_bankManager.TryTransferFromToBankAccount(source, target, message.Amount))
        {
            NotifyTransferResult(loaderUid, args.Actor, "bank-program-transfer-error-insufficient");
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
            return;
        }

        NotifyTransferResult(
            loaderUid,
            args.Actor,
            "bank-program-transfer-success",
            PopupType.Medium,
            ("amount", message.Amount),
            ("currencySymbol", GetCurrencySymbol(source.Comp)),
            ("target", target.Comp.AccountNumber));

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, new BankUiState(source.Comp.Balance));
    }

    private void OnItemRemoved(EntityUid uid, BankAccountComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (
            !TryComp<CartridgeLoaderComponent>(args.Container.Owner, out var cartridgeLoaderComponent))
        {
            return;
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(args.Container.Owner, new BankUiState(component.Balance));
    }

    private void OnItemInserted(EntityUid uid, BankAccountComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (
            !TryComp<CartridgeLoaderComponent>(args.Container.Owner, out var cartridgeLoaderComponent))
        {
            return;
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(args.Container.Owner, new BankUiState(component.Balance));
    }

    private void OnComponentInit(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentInit args)
    {
    }

    private void OnComponentRemove(EntityUid uid, BankCartridgeComponent bankCartrdigeComponent, ComponentRemove args)
    {
        UnlinkBankAccountFromCartridge(uid, null, bankCartrdigeComponent);
    }

    public void LinkBankAccountToCartridge(EntityUid uid, BankAccountComponent bankAccount,
        BankCartridgeComponent? bankCartrdigeComponent = null)
    {
        if (!Resolve(uid, ref bankCartrdigeComponent))
        {
            return;
        }

        bankCartrdigeComponent.LinkedBankAccount = bankAccount;
        //bankAccount.BankCartridge = uid;
    }

    public void UnlinkBankAccountFromCartridge(EntityUid uid, BankAccountComponent? bankAccount = null,
        BankCartridgeComponent? bankCartrdigeComponent = null)
    {
        if (!Resolve(uid, ref bankCartrdigeComponent, false))
        {
            return;
        }

        bankCartrdigeComponent.LinkedBankAccount = null;
    }

    private void OnChangeBankBalance(EntityUid uid, BankAccountComponent component, ChangeBankAccountBalanceEvent args)
    {
        if ((MetaData(uid).Flags & MetaDataFlags.InContainer) == 0)
            return;
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return;

        if (TryComp<RingerComponent>(parent, out var ringerComponent))
        {
            if (TryComp<PdaComponent>(parent, out var pda) && pda.SilentMode) // DS14
                return;

            _ringerSystem.RingerPlayRingtone((parent, ringerComponent));
            _cartridgeLoaderSystem?.UpdateCartridgeUiState(parent, new BankUiState(component.Balance));

            var player = Transform(parent).ParentUid;
            if (player.IsValid() && TryComp<ActorComponent>(player, out var actor))
            {
                var currencySymbol = "";
                if (_prototypeManager.TryIndex(component.CurrencyType, out CurrencyPrototype? p))
                    currencySymbol = Loc.GetString(p.CurrencySymbol);

                var change = (double) (args.ChangeAmount ?? 0);
                var changeAmount = $"{change}";
                switch (change)
                {
                    case > 0:
                    {
                        changeAmount = $"+{change}";
                        break;
                    }
                    case < 0:
                    {
                        changeAmount = $"-{change}";
                        break;
                    }
                }

                var wrappedMessage = Loc.GetString(
                    "bank-program-change-balance-notification",
                    ("balance", component.Balance), ("change", changeAmount),
                    ("currencySymbol", currencySymbol)
                );

                _popupSystem.PopupEntity(
                    wrappedMessage,
                    parent,
                    Filter.Entities(player),
                    true,
                    PopupType.Medium
                );

                _chatManager.ChatMessageToOne(
                    ChatChannel.Notifications,
                    wrappedMessage,
                    wrappedMessage,
                    EntityUid.Invalid,
                    false,
                    actor.PlayerSession.Channel);
            }
        }
        //UpdateUiState(uid, parent, component);
    }

    private bool TryGetPdaBankAccount(EntityUid loaderUid, out Entity<BankAccountComponent>? bankAccount)
    {
        bankAccount = null;

        if (!TryComp<PdaComponent>(loaderUid, out var pda) ||
            pda.IdSlot.Item is not { Valid: true } idCard)
            return false;

        if (!HasComp<IdCardComponent>(idCard))
            return false;

        return _bankManager.TryGetBankAccount(idCard, out bankAccount);
    }

    private string GetCurrencySymbol(BankAccountComponent account)
    {
        return _prototypeManager.TryIndex(account.CurrencyType, out CurrencyPrototype? prototype)
            ? Loc.GetString(prototype.CurrencySymbol)
            : string.Empty;
    }

    private void NotifyTransferResult(
        EntityUid loaderUid,
        EntityUid actorUid,
        string locKey,
        PopupType popupType = PopupType.SmallCaution,
        params (string, object)[] locArgs)
    {
        var message = Loc.GetString(locKey, locArgs);

        if (!actorUid.IsValid() || !TryComp<ActorComponent>(actorUid, out var actor))
        {
            _popupSystem.PopupEntity(message, loaderUid, popupType);
            return;
        }

        _popupSystem.PopupEntity(
            message,
            loaderUid,
            Filter.Entities(actorUid),
            true,
            popupType);

        _chatManager.ChatMessageToOne(
            ChatChannel.Notifications,
            message,
            message,
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel);
    }
}
