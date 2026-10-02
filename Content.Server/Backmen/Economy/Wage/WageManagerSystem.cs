// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Bed.Cryostorage;
using Content.Shared.Administration;
using Content.Shared.Backmen.Economy;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Economy.Wage;

public sealed class WagePaydayEvent : EntityEventArgs
{
    public FixedPoint2 Mod { get; set; } = 1;
    public FixedPoint2? Value { get; set; } = null;
    public readonly HashSet<Entity<BankAccountComponent>> WhiteListTo = new();
}

public sealed class WagePaydayPayout
{
    public uint Id { get; }
    public Entity<BankAccountComponent> FromAccountNumber { get; }
    public Entity<BankAccountComponent> ToAccountNumber { get; }
    public EntityUid? MindId { get; }
    public string? CharacterName { get; }
    public FixedPoint2 PayoutAmount { get; set; }

    public WagePaydayPayout(uint id,
        Entity<BankAccountComponent> fromAccountNumber,
        Entity<BankAccountComponent> toAccountNumber,
        EntityUid? mindId = null,
        string? characterName = null)
    {
        Id = id;
        FromAccountNumber = fromAccountNumber;
        ToAccountNumber = toAccountNumber;
        MindId = mindId;
        CharacterName = characterName;
    }
}

public sealed class WageManagerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly BankManagerSystem _bankManagerSystem = default!;

    private uint _nextId = 1;

    [ViewVariables(VVAccess.ReadWrite)]
    public readonly Dictionary<uint, WagePaydayPayout> PayoutsList = new();

    private readonly Dictionary<EntityUid, (ProtoId<DepartmentPrototype> Department, string Name)> _departmentNames = new();

    public bool TryGetPayout(uint id, [NotNullWhen(true)] out WagePaydayPayout? payout)
        => PayoutsList.TryGetValue(id, out payout);

    public IReadOnlyDictionary<EntityUid, (ProtoId<DepartmentPrototype> Department, string Name)> DepartmentNames
        => _departmentNames;

    [PublicAPI]
    public void RegisterDepartmentAccount(EntityUid account,
        ProtoId<DepartmentPrototype> department,
        string name)
    {
        _departmentNames[account] = (department, name);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public bool WagesEnabled { get; private set; }
    //private void SetEnabled(bool value) => WagesEnabled = value;
    private void SetEnabled(bool value)
    {
        WagesEnabled = value;
    }
    public override void Initialize()
    {
        base.Initialize();
        _configurationManager.OnValueChanged(Shared.Backmen.CCVar.CCVars.EconomyWagesEnabled, SetEnabled, true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<WagePaydayEvent>(OnPayday);
        SubscribeLocalEvent<MindComponent, MindRemovedMessage>(OnMindRemoved, after: new[] { typeof(CryostorageSystem) });
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        PayoutsList.Clear();
        _departmentNames.Clear();
        _nextId = 1;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _configurationManager.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.EconomyWagesEnabled, SetEnabled);
    }

    public void OnPayday(WagePaydayEvent ev)
    {
        foreach (var payout in PayoutsList.Values.ToArray())
        {
            // бонусная зп на отдел?
            if (ev.WhiteListTo.Count > 0 && !ev.WhiteListTo.Contains(payout.ToAccountNumber))
            {
                continue;
            }
            var val = ev.Value ?? payout.PayoutAmount;

            if (!IsPayoutValid(payout))
            {
                PayoutsList.Remove(payout.Id);
                continue;
            }

            _bankManagerSystem.TryTransferFromToBankAccount(
                payout.FromAccountNumber,
                payout.ToAccountNumber,
                val * ev.Mod);
        }
    }

    private bool IsPayoutValid(WagePaydayPayout payout)
    {
        if (TerminatingOrDeleted(payout.FromAccountNumber) || TerminatingOrDeleted(payout.ToAccountNumber))
            return false;

        if (payout.MindId is { } mindId)
        {
            if (TerminatingOrDeleted(mindId) ||
                !TryComp<MindComponent>(mindId, out var mind) ||
                mind.OwnedEntity == null)
                return false;
        }

        return true;
    }

    [PublicAPI]
    public int PruneInvalidPayouts()
    {
        var removed = 0;
        foreach (var id in PayoutsList.Keys.ToArray())
        {
            if (!IsPayoutValid(PayoutsList[id]))
            {
                PayoutsList.Remove(id);
                removed++;
            }
        }

        return removed;
    }

    private void OnMindRemoved(Entity<MindComponent> ent, ref MindRemovedMessage args)
    {
        foreach (var id in PayoutsList.Values
                     .Where(p => p.MindId == ent.Owner)
                     .Select(p => p.Id)
                     .ToArray())
        {
            PayoutsList.Remove(id);
        }
    }

    public bool TryAddAccountToWagePayoutList(Entity<BankAccountComponent> bankAccount,
        JobPrototype jobPrototype,
        EntityUid? mindId = null,
        string? characterName = null)
    {
        if (jobPrototype.WageDepartment == null ||
            !_prototypeManager.TryIndex(jobPrototype.WageDepartment, out DepartmentPrototype? department))
            return false;

        if (!_bankManagerSystem.TryGetBankAccount(department.AccountNumber, out var departmentBankAccount))
            return false;

        RemovePayoutsFor(mindId, bankAccount);

        var newPayout = new WagePaydayPayout(_nextId++, departmentBankAccount.Value, bankAccount,
            mindId, characterName)
        {
            PayoutAmount = jobPrototype.Wage
        };

        PayoutsList[newPayout.Id] = newPayout;
        return true;
    }

    private void RemovePayoutsFor(EntityUid? mindId, Entity<BankAccountComponent> bankAccount)
    {
        foreach (var id in PayoutsList.Values
                     .Where(p => (mindId != null && p.MindId == mindId)
                                 || p.ToAccountNumber.Owner == bankAccount.Owner)
                     .Select(p => p.Id)
                     .ToArray())
        {
            PayoutsList.Remove(id);
        }
    }
}
