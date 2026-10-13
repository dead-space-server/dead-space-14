using System.Linq;
using Content.Shared.Cargo.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.UserInterface;
// DS14-start
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
// DS14-end

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private bool _allowPrimaryAccountAllocation;
    private bool _allowPrimaryCutAdjustment;

    public void InitializeFunds()
    {
        SubscribeLocalEvent<CargoOrderConsoleComponent, CargoConsoleWithdrawFundsMessage>(OnWithdrawFunds);
        SubscribeLocalEvent<CargoOrderConsoleComponent, CargoConsoleToggleLimitMessage>(OnToggleLimit);
        SubscribeLocalEvent<FundingAllocationConsoleComponent, SetFundingAllocationBuiMessage>(OnSetFundingAllocation);
        SubscribeLocalEvent<FundingAllocationConsoleComponent, BeforeActivatableUIOpenEvent>(OnFundAllocationBuiOpen);

        _cfg.OnValueChanged(CCVars.AllowPrimaryAccountAllocation, enabled => { _allowPrimaryAccountAllocation = enabled; }, true);
        _cfg.OnValueChanged(CCVars.AllowPrimaryCutAdjustment, enabled => { _allowPrimaryCutAdjustment = enabled; }, true);
    }

    private void OnWithdrawFunds(Entity<CargoOrderConsoleComponent> ent, ref CargoConsoleWithdrawFundsMessage args)
    {
        if (_station.GetOwningStation(ent) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        if (args.Account == ent.Comp.Account ||
            args.Amount <= 0 ||
            args.Amount > GetBalanceFromAccount((station, bank), ent.Comp.Account) * ent.Comp.TransferLimit)
            return;

        if (Timing.CurTime < ent.Comp.NextAccountActionTime)
            return;

        if (!_accessReaderSystem.IsAllowed(args.Actor, ent))
        {
            ConsolePopup(args.Actor, Loc.GetString("cargo-console-order-not-allowed"));
            PlayDenySound(ent, ent.Comp);
            return;
        }

        ent.Comp.NextAccountActionTime = Timing.CurTime + ent.Comp.AccountActionDelay;
        UpdateBankAccount((station, bank), -args.Amount,  ent.Comp.Account, dirty: false);
        _audio.PlayPvs(ApproveSound, ent);

        var tryGetIdentityShortInfoEvent = new TryGetIdentityShortInfoEvent(ent, args.Actor);
        RaiseLocalEvent(tryGetIdentityShortInfoEvent);

        var ourAccount = _protoMan.Index(ent.Comp.Account);
        if (args.Account == null)
        {
            var stackPrototype = _protoMan.Index(ent.Comp.CashType);
            _stack.SpawnAtPosition(args.Amount, stackPrototype, Transform(ent).Coordinates);

            if (!_emag.CheckFlag(ent, EmagType.Interaction))
            {
                var msg = Loc.GetString("cargo-console-fund-withdraw-broadcast",
                    ("name", tryGetIdentityShortInfoEvent.Title ?? Loc.GetString("cargo-console-fund-transfer-user-unknown")),
                    ("amount", args.Amount),
                    ("name1", Loc.GetString(ourAccount.Name)),
                    ("code1", Loc.GetString(ourAccount.Code)));
                _radio.SendRadioMessage(ent, msg, ourAccount.RadioChannel, ent, escapeMarkup: false);
            }
        }
        else
        {
            var otherAccount = _protoMan.Index(args.Account.Value);
            UpdateBankAccount((station, bank), args.Amount, args.Account.Value);

            if (!_emag.CheckFlag(ent, EmagType.Interaction))
            {
                var msg = Loc.GetString("cargo-console-fund-transfer-broadcast",
                    ("name", tryGetIdentityShortInfoEvent.Title ?? Loc.GetString("cargo-console-fund-transfer-user-unknown")),
                    ("amount", args.Amount),
                    ("name1", Loc.GetString(ourAccount.Name)),
                    ("code1", Loc.GetString(ourAccount.Code)),
                    ("name2", Loc.GetString(otherAccount.Name)),
                    ("code2", Loc.GetString(otherAccount.Code)));
                _radio.SendRadioMessage(ent, msg, ourAccount.RadioChannel, ent, escapeMarkup: false);
                _radio.SendRadioMessage(ent, msg, otherAccount.RadioChannel, ent, escapeMarkup: false);
            }
        }
    }

    private void OnToggleLimit(Entity<CargoOrderConsoleComponent> ent, ref CargoConsoleToggleLimitMessage args)
    {
        if (!_accessReaderSystem.FindAccessTags(args.Actor).Intersect(ent.Comp.RemoveLimitAccess).Any())
        {
            ConsolePopup(args.Actor, Loc.GetString("cargo-console-order-not-allowed"));
            PlayDenySound(ent, ent.Comp);
            return;
        }

        _audio.PlayPvs(ent.Comp.ToggleLimitSound, ent);
        ent.Comp.TransferUnbounded = !ent.Comp.TransferUnbounded;
        Dirty(ent);
    }


    private void OnSetFundingAllocation(Entity<FundingAllocationConsoleComponent> ent, ref SetFundingAllocationBuiMessage args)
    {
        if (_station.GetOwningStation(ent) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        var expectedCount = _allowPrimaryAccountAllocation ? bank.RevenueDistribution.Count : bank.RevenueDistribution.Count - 1;
        if (args.Percents.Count != expectedCount)
            return;

        var differs = false;
        foreach (var (account, percent) in args.Percents)
        {
            if (percent != (int) Math.Round(bank.RevenueDistribution[account] * 100))
            {
                differs = true;
                break;
            }
        }
        differs = differs || args.PrimaryCut != bank.PrimaryCut || args.LockboxCut != bank.LockboxCut;

        if (!differs)
            return;

        if (args.Percents.Values.Sum() != 100)
            return;

        var primaryCut = bank.RevenueDistribution[bank.PrimaryAccount];
        bank.RevenueDistribution.Clear();
        foreach (var (account, percent )in args.Percents)
        {
            bank.RevenueDistribution.Add(account, percent / 100.0);
        }
        if (!_allowPrimaryAccountAllocation)
        {
            bank.RevenueDistribution.Add(bank.PrimaryAccount, 0);
        }

        if (_allowPrimaryCutAdjustment && args.PrimaryCut is >= 0.0 and <= 1.0)
        {
            bank.PrimaryCut = args.PrimaryCut;
        }
        if (_lockboxCutEnabled && args.LockboxCut is >= 0.0 and <= 1.0)
        {
            bank.LockboxCut = args.LockboxCut;
        }

        Dirty(station, bank);

        _audio.PlayPvs(ent.Comp.SetDistributionSound, ent);
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set station {ToPrettyString(station)} fund distribution: {string.Join(',', bank.RevenueDistribution.Select(p => $"{p.Key}: {p.Value}").ToList())}, primary cut: {bank.PrimaryCut}, lockbox cut: {bank.LockboxCut}");
    }

    private void OnFundAllocationBuiOpen(Entity<FundingAllocationConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (_station.GetOwningStation(ent) is { } station)
            _uiSystem.SetUiState(ent.Owner, FundingAllocationConsoleUiKey.Key, new FundingAllocationConsoleBuiState(GetNetEntity(station)));
    }

    // DS14-start
    #region Revenue distribution

    /// <summary>
    /// All the accounts that have a cargo order console somewhere on the given station.
    /// </summary>
    private HashSet<ProtoId<CargoAccountPrototype>> GetStationOrderAccounts(EntityUid station)
    {
        var accounts = new HashSet<ProtoId<CargoAccountPrototype>>();

        var query = EntityQueryEnumerator<CargoOrderConsoleComponent>();
        while (query.MoveNext(out var uid, out var orderConsole))
        {
            if (_station.GetOwningStation(uid) == station)
                accounts.Add(orderConsole.Account);
        }

        return accounts;
    }

    /// <summary>
    /// The same as the shared account distribution, except that accounts without a cargo order console on the station
    /// are given nothing. Their share is handed to the primary account instead, so that no money gets silently
    /// deleted from the station's income.
    /// </summary>
    public Dictionary<ProtoId<CargoAccountPrototype>, double> CreateStationAccountDistribution(
        Entity<StationBankAccountComponent> stationBank)
    {
        var bank = stationBank.Comp;
        var revenue = GetStationRevenueDistribution(stationBank);

        var distribution = new Dictionary<ProtoId<CargoAccountPrototype>, double>
        {
            { bank.PrimaryAccount, bank.PrimaryCut }
        };

        var remaining = 1.0 - bank.PrimaryCut;
        foreach (var (account, percentage) in revenue)
            distribution[account] = distribution.GetValueOrDefault(account) + remaining * percentage;

        return distribution;
    }

    /// <summary>
    /// The station's <see cref="StationBankAccountComponent.RevenueDistribution"/>, normalized so that it always sums
    /// up to 1.0, with the share of every account that has no cargo order console on this station folded into the
    /// primary account.
    /// </summary>
    public Dictionary<ProtoId<CargoAccountPrototype>, double> GetStationRevenueDistribution(
        Entity<StationBankAccountComponent> stationBank)
    {
        var bank = stationBank.Comp;
        var accounts = GetStationOrderAccounts(stationBank.Owner);

        var revenue = new Dictionary<ProtoId<CargoAccountPrototype>, double>();
        var total = 0.0;
        var kept = 0.0;

        foreach (var (account, percentage) in bank.RevenueDistribution)
        {
            total += percentage;

            if (account != bank.PrimaryAccount && !accounts.Contains(account))
                continue;

            revenue[account] = revenue.GetValueOrDefault(account) + percentage;
            kept += percentage;
        }

        if (total <= 0.0)
            return new Dictionary<ProtoId<CargoAccountPrototype>, double> { { bank.PrimaryAccount, 1.0 } };

        if (!MathHelper.CloseTo(total, 1.0))
        {
            var keys = revenue.Keys.ToArray();
            foreach (var account in keys)
                revenue[account] /= total;

            kept /= total;
            total = 1.0;
        }

        if (kept < total)
            revenue[bank.PrimaryAccount] = revenue.GetValueOrDefault(bank.PrimaryAccount) + total - kept;

        return revenue;
    }

    #endregion
// DS14-end
}
