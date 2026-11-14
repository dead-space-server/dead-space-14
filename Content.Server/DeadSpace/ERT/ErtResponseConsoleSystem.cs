// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Access.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.DeadSpace.ERT.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.ERT;

public sealed class ErtResponseConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly ErtResponseSystem _ertResponseSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtResponseConsoleComponent, ErtResponseConsoleUiButtonPressedMessage>(OnButtonPressed);
        SubscribeLocalEvent<ErtResponseConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<ErtResponseConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ErtResponseConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ErtResponseConsoleComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<ErtResponseConsoleComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<ErtResponseConsoleComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
    }

    private void OnButtonPressed(EntityUid uid, ErtResponseConsoleComponent component, ErtResponseConsoleUiButtonPressedMessage args)
    {
        if (!_powerReceiver.IsPowered(uid) || !IsConsoleAuthorized((uid, component)) || string.IsNullOrEmpty(args.Team))
            return;

        // Only a team this console offers: the id comes from the client, and other teams are for admins to send.
        if (!component.Teams.Contains(args.Team))
            return;

        // The reason goes into announcements and admin alerts: no longer than a chat message.
        var callReason = args.CallReason?.Trim();
        var maxReasonLength = _cfg.GetCVar(CCVars.ChatMaxMessageLength);
        if (callReason != null && callReason.Length > maxReasonLength)
            callReason = callReason[..maxReasonLength];

        var station = _stationSystem.GetOwningStation(uid);
        var requesterName = args.Actor is { Valid: true } actor
            ? GetRequesterName(actor)
            : Loc.GetString("ert-console-requester-unknown");

        if (args.Button != ErtResponseConsoleUiButton.ResponseErt)
            return;

        string? reason;
        bool success;

        if (component.UseApprovalWorkflow)
        {
            success = _ertResponseSystem.TrySubmitConsoleRequest(
                args.Team,
                station,
                requesterName,
                uid,
                out reason,
                callReason);
        }
        else
        {
            success = _ertResponseSystem.TryCallErt(
                args.Team,
                station,
                out reason,
                callReason: callReason);
        }

        if (!success)
        {
            _chatSystem.TrySendInGameICMessage(
                uid,
                reason ?? Loc.GetString("ert-response-call-cancel"),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                true);
        }
        else if (component.UseApprovalWorkflow)
        {
            _chatSystem.TrySendInGameICMessage(
                uid,
                Loc.GetString("ert-response-call-submitted"),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                true);
        }

        UpdateUserInterface((uid, component));
    }

    private void OnMapInit(EntityUid uid, ErtResponseConsoleComponent component, MapInitEvent args)
    {
        component.IsAuthorized = IsConsoleAuthorized((uid, component));
        Dirty(uid, component);
    }

    private void OnItemSlotInsertAttempt(EntityUid uid, ErtResponseConsoleComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (!IsAuthorizationSlot(args.Slot))
            return;

        if (IsAllowedAuthorizationCard(args.Item))
            return;

        args.Cancelled = true;
        if (args.User != null)
            _popup.PopupEntity(Loc.GetString("ert-console-auth-card-invalid"), uid, args.User.Value);
    }

    private void OnCardInserted(EntityUid uid, ErtResponseConsoleComponent component, ref EntInsertedIntoContainerMessage args)
    {
        OnAuthorizationCardsChanged((uid, component));
    }

    private void OnCardRemoved(EntityUid uid, ErtResponseConsoleComponent component, ref EntRemovedFromContainerMessage args)
    {
        OnAuthorizationCardsChanged((uid, component));
    }

    private void OnAuthorizationCardsChanged(Entity<ErtResponseConsoleComponent> console)
    {
        var isAuthorized = IsConsoleAuthorized(console);
        if (console.Comp.IsAuthorized != isAuthorized)
        {
            console.Comp.IsAuthorized = isAuthorized;
            Dirty(console);
        }

        if (!isAuthorized && TryComp<UserInterfaceComponent>(console, out var ui))
            _uiSystem.CloseUis((console, ui));

        UpdateUserInterface(console);
    }

    private void OnPowerChanged(EntityUid uid, ErtResponseConsoleComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    private void OnUiOpened(EntityUid uid, ErtResponseConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    public void UpdateUserInterface(Entity<ErtResponseConsoleComponent> console)
    {
        if (!TryComp<UserInterfaceComponent>(console, out var userInterface))
            return;

        if (!_uiSystem.HasUi(console, ErtResponseConsoleUiKey.Key, userInterface))
            return;

        if (!_powerReceiver.IsPowered(console))
        {
            _uiSystem.CloseUis((console, userInterface));
            return;
        }

        var state = GetUserInterfaceState(console);
        _uiSystem.SetUiState((console, userInterface), ErtResponseConsoleUiKey.Key, state);
    }

    private ErtResponseConsoleBoundUserInterfaceState GetUserInterfaceState(Entity<ErtResponseConsoleComponent> console)
    {
        var balance = _ertResponseSystem.GetBalance();
        var isAuthorized = console.Comp.IsAuthorized;

        return new ErtResponseConsoleBoundUserInterfaceState(
            console.Comp.Teams,
            balance,
            isAuthorized,
            isAuthorized ? null : Loc.GetString("ert-console-auth-required"));
    }

    private bool IsAuthorizationSlot(ItemSlot slot)
    {
        return slot.ID == ErtResponseConsoleComponent.AuthSlotAId ||
               slot.ID == ErtResponseConsoleComponent.AuthSlotBId;
    }

    private bool IsAllowedAuthorizationCard(EntityUid uid)
    {
        return GetAuthorizationCardKind(uid) != AuthorizationCardKind.Invalid;
    }

    private bool IsConsoleAuthorized(Entity<ErtResponseConsoleComponent> console)
    {
        if (!TryComp<ItemSlotsComponent>(console.Owner, out var itemSlots))
            return false;

        if (!_itemSlots.TryGetSlot(console, ErtResponseConsoleComponent.AuthSlotAId, out var slotA) ||
            !_itemSlots.TryGetSlot(console, ErtResponseConsoleComponent.AuthSlotBId, out var slotB))
            return false;

        var firstCard = GetAuthorizationCardKind(slotA.Item);
        var secondCard = GetAuthorizationCardKind(slotB.Item);

        if (firstCard == AuthorizationCardKind.Invalid || secondCard == AuthorizationCardKind.Invalid)
            return false;

        if (firstCard == AuthorizationCardKind.HeadOfSecurity &&
            secondCard == AuthorizationCardKind.HeadOfSecurity)
            return false;

        return firstCard == AuthorizationCardKind.Captain ||
               secondCard == AuthorizationCardKind.Captain;
    }

    private AuthorizationCardKind GetAuthorizationCardKind(EntityUid? uid)
    {
        if (uid is not { Valid: true } entity)
            return AuthorizationCardKind.Invalid;

        return MetaData(entity).EntityPrototype?.ID switch
        {
            "CaptainIDCard" => AuthorizationCardKind.Captain,
            "HoSIDCard" => AuthorizationCardKind.HeadOfSecurity,
            _ => AuthorizationCardKind.Invalid,
        };
    }

    private string GetRequesterName(EntityUid actor)
    {
        var name = Name(actor);
        if (!_idCardSystem.TryFindIdCard(actor, out var idCard) ||
            string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
        {
            return name;
        }

        return Loc.GetString(
            "ert-console-requester-name-with-job",
            ("name", name),
            ("job", idCard.Comp.LocalizedJobTitle));
    }
}

public enum AuthorizationCardKind : byte
{
    Invalid,
    Captain,
    HeadOfSecurity,
}
