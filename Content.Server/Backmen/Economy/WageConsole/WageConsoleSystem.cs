// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.Economy.Wage;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.Backmen.Economy;
using Content.Shared.Backmen.Economy.WageConsole;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Economy.WageConsole;

public sealed class WageConsoleSystem : SharedWageConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WageManagerSystem _wageManager = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WageConsoleComponent,ActivatableUIOpenAttemptEvent>(OnTryOpenUi);
        Subs.BuiEvents<WageConsoleComponent>(WageUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<OpenWageRowMsg>(OnOpenWageRow);
            subs.Event<SaveEditedWageRowMsg>(OnEditWageRow);
            subs.Event<OpenBonusWageMsg>(OnOpenBonusRow);
            subs.Event<BonusWageRowMsg>(OnBonusMsg);
        });
    }

    private void OnTryOpenUi(Entity<WageConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actorComponent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.User, PopupType.Medium);
            args.Cancel();
            return;
        }
        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.User, PopupType.Medium);
            args.Cancel();
        }
    }

    private void OnBonusMsg(Entity<WageConsoleComponent> ent, ref BonusWageRowMsg args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actorComponent))
        {
            return;
        }

        if (!_access.IsAllowed(args.Actor, ent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.Actor, PopupType.Medium);
            return;
        }

        if (args.Id == null || !_wageManager.TryGetPayout(args.Id.Value, out var wagePayout))
        {
            return;
        }

        if (args.Wage <= 0)
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-invalid-amount"), args.Actor, PopupType.Medium);
            return;
        }

        _adminLogger.Add(LogType.Transactions, LogImpact.Extreme,
            $"wage, player {ToPrettyString(args.Actor):player} use bonus on accountId {wagePayout.ToAccountNumber.Comp.AccountNumber} with name {wagePayout.ToAccountNumber:entity} and add {args.Wage}");

        QueueLocalEvent(new WagePaydayEvent()
        {
            Mod = 1,
            Value = args.Wage,
            WhiteListTo = { wagePayout.ToAccountNumber }
        });

        UpdateUserInterface(ent);
    }

    private void OnOpenBonusRow(Entity<WageConsoleComponent> ent, ref OpenBonusWageMsg args)
    {
        if (args.Id == null || !_wageManager.TryGetPayout(args.Id.Value, out var wagePayout))
        {
            return;
        }

        _ui.SetUiState(ent.Owner, WageUiKey.Key, new OpenBonusWageConsoleUi
        {
            Row = BuildRow(wagePayout)
        });
    }

    private void OnEditWageRow(Entity<WageConsoleComponent> ent, ref SaveEditedWageRowMsg args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actorComponent))
        {
            return;
        }

        if (!_access.IsAllowed(args.Actor, ent))
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-insufficient-access"), args.Actor, PopupType.Medium);
            return;
        }

        if (args.Id == null || !_wageManager.TryGetPayout(args.Id.Value, out var wagePayout))
        {
            return;
        }

        if (args.Wage < 0)
        {
            _popup.PopupCursor(Loc.GetString("wageconsole-invalid-amount"), args.Actor, PopupType.Medium);
            return;
        }

        _adminLogger.Add(LogType.Transactions, LogImpact.Extreme,
            $"wage, player {ToPrettyString(args.Actor):player} use edit on accountId {wagePayout.ToAccountNumber.Comp.AccountNumber} with name {wagePayout.ToAccountNumber.Owner:entity} and set payout to {args.Wage}");

        wagePayout.PayoutAmount = args.Wage;
        UpdateUserInterface(ent);
    }

    private void OnOpenWageRow(Entity<WageConsoleComponent> ent, ref OpenWageRowMsg args)
    {
        if (args.Id == null || !_wageManager.TryGetPayout(args.Id.Value, out var wagePayout))
        {
            return;
        }

        var row = BuildRow(wagePayout);
        if (row == null)
            return;

        _ui.SetUiState(ent.Owner, WageUiKey.Key, new OpenEditWageConsoleUi
        {
            Row = row
        });
    }

    private void UpdateUserInterface(Entity<WageConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    private string GetAccountName(Entity<BankAccountComponent> account)
    {
        if (_wageManager.DepartmentNames.TryGetValue(account.Owner, out var department))
            return department.Name;

        if (!string.IsNullOrEmpty(account.Comp.AccountName))
            return account.Comp.AccountName;

        if (HasComp<MetaDataComponent>(account.Owner))
            return MetaData(account.Owner).EntityName;

        return account.Comp.AccountNumber;
    }

    private UpdateWageRow? BuildRow(WagePaydayPayout wagePayout)
    {
        if (!TryComp(wagePayout.FromAccountNumber, out MetaDataComponent? mdFrom) ||
           !TryComp(wagePayout.ToAccountNumber, out MetaDataComponent? mdTp))
            return null;

        return new UpdateWageRow
        {
            Id = wagePayout.Id,

            FromId = GetNetEntity(wagePayout.FromAccountNumber, mdFrom),
            FromName = GetAccountName(wagePayout.FromAccountNumber),
            FromAccount = wagePayout.FromAccountNumber.Comp.AccountNumber,
            FromBalance = wagePayout.FromAccountNumber.Comp.Balance,

            ToId = GetNetEntity(wagePayout.ToAccountNumber, mdTp),
            ToName = GetAccountName(wagePayout.ToAccountNumber),
            ToAccount = wagePayout.ToAccountNumber.Comp.AccountNumber,
            ToBalance = wagePayout.ToAccountNumber.Comp.Balance,

            Wage = wagePayout.PayoutAmount,
        };
    }

    private void UpdateUserInterface(Entity<WageConsoleComponent> ent)
    {
        var msg = new UpdateWageConsoleUi();

        _wageManager.PruneInvalidPayouts();

        foreach (var wagePayout in _wageManager.PayoutsList.Values)
        {
            var row = BuildRow(wagePayout);
            if (row != null)
                msg.Records.Add(row);
        }

        _ui.SetUiState(ent.Owner, WageUiKey.Key, msg);
    }
}
