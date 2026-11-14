using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Verbs;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Robust.Shared.Timing;
using Content.Shared.Access.Systems;
using Robust.Shared.Utility;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Content.Server.Radio.EntitySystems;
using Robust.Shared.Random;
using System.Text;
using Content.Shared.Mind;
using Content.Shared.Foldable;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaHackBeaconSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly NinjaStealMoneyObjectiveSystem _ninjaStealMoneyObjective = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHackBeaconComponent, FoldedEvent>(OnFolded);
        SubscribeLocalEvent<NinjaHackBeaconComponent, FoldAttemptEvent>(OnFoldAttempt);

        SubscribeLocalEvent<NinjaHackBeaconComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<NinjaHackBeaconComponent, ExaminedEvent>(OnExamined);
    }

    private void SetActive(Entity<NinjaHackBeaconComponent> ent, bool active, EntityUid? user = null)
    {
        if (ent.Comp.Active == active)
            return;

        if (user != null && !_accessReaderSystem.IsAllowed(user.Value, ent))
            return;

        if (active)
        {
            if (IsFolded(ent))
                return;

            if (_station.GetOwningStation(ent) == null)
                return;

            if (user == null)
                return;

            if (!_mind.TryGetMind(user.Value, out var mindId, out _))
                return;

            ent.Comp.Mind = mindId;
            ent.Comp.NextMineTime = _timing.CurTime + ent.Comp.MineDelay;
        }

        ent.Comp.Active = active;
        _ambient.SetAmbience(ent, active);
    }

    private bool IsFolded(EntityUid uid)
    {
        return TryComp<FoldableComponent>(uid, out var foldable) && foldable.IsFolded;
    }

    private void OnFoldAttempt(Entity<NinjaHackBeaconComponent> ent, ref FoldAttemptEvent args)
    {
        if (ent.Comp.Active && !args.Cancelled)
            args.Cancelled = true;
    }

    private void OnFolded(Entity<NinjaHackBeaconComponent> ent, ref FoldedEvent args)
    {
        if (args.IsFolded)
            SetActive(ent, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaHackBeaconComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active)
                continue;

            if (_timing.CurTime < comp.NextMineTime)
                continue;

            comp.NextMineTime = _timing.CurTime + comp.MineDelay;

            if (IsFolded(uid))
            {
                SetActive((uid, comp), false);
                continue;
            }

            var station = _station.GetOwningStation(uid);
            if (station == null)
            {
                SetActive((uid, comp), false);
                continue;
            }

            var withdrawn = AnnounceAndWithdraw(station.Value, comp.MineAtOnce);
            if (withdrawn <= 0)
                continue;

            var coins = withdrawn / comp.ConversionRate;

            _store.TryAddCurrency(new Dictionary<string, FixedPoint2> { { comp.SpiderCoinCurrencyPrototype, coins } }, uid);
            if (comp.Mind != null && TryComp<MindComponent>(comp.Mind.Value, out var mindComponent))
            {
                foreach (var objective in mindComponent.Objectives)
                {
                    if (!TryComp<NinjaStealMoneyConditionComponent>(objective, out var objectiveComp))
                        continue;
                    _ninjaStealMoneyObjective.AddMoneyCount(objectiveComp, withdrawn);
                }
            }
        }
    }

    private void OnGetVerbs(Entity<NinjaHackBeaconComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (IsFolded(ent))
            return;

        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Text = ent.Comp.Active
                ? Loc.GetString("ninja-hack-beacon-verb-disable")
                : Loc.GetString("ninja-hack-beacon-verb-enable"),
            Act = () => SetActive(ent, !ent.Comp.Active, user),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
        });
    }

    private void OnExamined(Entity<NinjaHackBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!ent.Comp.Active)
        {
            args.PushMarkup(Loc.GetString("ninja-hack-beacon-inactive"));
            return;
        }

        var left = ent.Comp.NextMineTime - _timing.CurTime;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;

        args.PushMarkup(Loc.GetString("ninja-hack-beacon-next-mine",
            ("seconds", (int)Math.Ceiling(left.TotalSeconds))));
    }

    private int AnnounceAndWithdraw(
    Entity<StationBankAccountComponent?> station,
    int amount,
    bool dirty = true)
    {
        if (amount <= 0)
            return 0;

        if (!Resolve(station, ref station.Comp))
            return 0;

        var accounts = station.Comp.Accounts;

        var balances = new Dictionary<ProtoId<CargoAccountPrototype>, int>();
        foreach (var (account, balance) in accounts)
        {
            if (balance > 0)
                balances[account] = balance;
        }

        var plan = new Dictionary<ProtoId<CargoAccountPrototype>, int>();
        var active = new List<ProtoId<CargoAccountPrototype>>(balances.Keys);
        var remaining = amount;

        while (remaining > 0 && active.Count > 0)
        {
            var share = remaining / active.Count;
            var extra = remaining % active.Count;

            for (var i = 0; i < active.Count; i++)
            {
                var account = active[i];
                var want = share + (i < extra ? 1 : 0);
                var taken = Math.Min(balances[account], want);

                balances[account] -= taken;
                remaining -= taken;
                plan[account] = plan.GetValueOrDefault(account) + taken;
            }

            active.RemoveAll(a => balances[a] <= 0);
        }

        var total = amount - remaining;

        if (total <= 0)
            return 0;

        AnnounceTransfers(station, plan);

        foreach (var (account, taken) in plan)
        {
            accounts[account] -= taken;
        }

        var ev = new BankBalanceUpdatedEvent(station, accounts);
        RaiseLocalEvent(station, ref ev, true);

        if (dirty)
            Dirty(station);

        return total;
    }

    private void AnnounceTransfers(
    EntityUid station,
    Dictionary<ProtoId<CargoAccountPrototype>, int> plan)
    {
        var consoles = new Dictionary<ProtoId<CargoAccountPrototype>, List<EntityUid>>();
        var query = EntityQueryEnumerator<CargoOrderConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.IsTaipan || !plan.ContainsKey(console.Account))
                continue;

            if (MetaData(uid).EntityPrototype?.ID == GameTicker.AdminObserverPrototypeName.Id)
                continue;

            if (_station.GetOwningStation(uid) != station)
                continue;

            if (!consoles.TryGetValue(console.Account, out var list))
                consoles[console.Account] = list = new List<EntityUid>();

            list.Add(uid);
        }

        foreach (var (accountId, taken) in plan)
        {
            if (taken <= 0)
                continue;

            if (!consoles.TryGetValue(accountId, out var list) || list.Count == 0)
                continue;

            var console = _random.Pick(list);
            var account = _protoMan.Index(accountId);

            var msg = Loc.GetString("ninja-hack-beacon-transfer-broadcast",
                ("amount", taken),
                ("name", Loc.GetString(account.Name)),
                ("code", Loc.GetString(account.Code)),
                ("position", GenerateGarbage(_navMap.GetNearestBeaconString(console))));

            _radio.SendRadioMessage(console, msg, account.RadioChannel, console, escapeMarkup: false);
        }
    }

    private string GenerateGarbage(string input, float percent = 0.25f)
    {
        if (string.IsNullOrEmpty(input) || percent <= 0f)
            return input;
        percent = Math.Clamp(percent, 0f, 1f);
        const string chars = "&@(!)#$%^*?~+=<>/|";
        var sb = new StringBuilder(input);
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == '[')
            {
                var close = input.IndexOf(']', i + 1);
                if (close >= 0)
                {
                    i = close + 1;
                    continue;
                }
            }
            if (!char.IsWhiteSpace(input[i]) && _random.NextDouble() < percent)
                sb[i] = chars[_random.Next(chars.Length)];
            i++;
        }
        return sb.ToString();
    }
}