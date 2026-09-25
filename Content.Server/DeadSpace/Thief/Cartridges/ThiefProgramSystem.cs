using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.DeadSpace.Thief.Cartridges;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Thief;
using Content.Shared.DeadSpace.Thief.Prototypes;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Thief.Cartridges;

/// <summary>
/// DS14: Server logic of the ВорПРО program — the thief's PDA program.
///
/// The program is unlocked by inserting a tool (an item that fits in a utility tool
/// belt) into the PDA's tool slot: only thieves receive the program. When the tool is
/// taken out of the PDA again, the program is deleted. It provides two tabs:
/// "Запросы" (requests) and "Аплинк" (uplink).
///
/// Requests: the thief accepts a request for N items of a steal target group, brings the items
/// to their linked thief beacon and sells them for dirty credits. Delivering before the deadline
/// grants a bonus, delivering late reduces the price.
///
/// Uplink: dirty credits can be spent on thief equipment or exchanged into regular credits 1:1.
///
/// Dirty credits are physical stackable cash (<see cref="StackPrototype"/> DirtyCredit), they are
/// stored in the thief's inventory, not on the PDA.
/// </summary>
public sealed class ThiefProgramSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    /// <summary>Entity spawned as dirty credits payout.</summary>
    public static readonly EntProtoId DirtyCashProto = "DirtyCash";

    /// <summary>Regular credit bill used when exchanging dirty credits 1:1.</summary>
    public static readonly EntProtoId CleanCashProto = "SpaceCash";

    /// <summary>The ВорПРО program entity that gets installed onto the thief's PDA.</summary>
    public static readonly EntProtoId ThiefProgramId = "ThiefPdaProgram";

    private static readonly SoundPathSpecifier SellSound = new("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundPathSpecifier ErrorSound = new("/Audio/Machines/beep.ogg");

    /// <summary>Purchase sound, the same one the uplink store uses.</summary>
    private static readonly SoundPathSpecifier BuySound = new("/Audio/Effects/kaching.ogg");

    /// <summary>How often the ui state is refreshed so timers, balance and the round goal stay live.</summary>
    private const float UiRefreshInterval = 1f;

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefPdaProgramComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<ThiefPdaProgramComponent, CartridgeUiReadyEvent>(OnUiReady);

        // DS14: Wire unsubscribe too — the ВорПРО unlock (insert the tool into the PDA's
        // tool slot) is disabled for normal players (kept for the future thief rework).
        // // Broadcast subscription: inserting a tool into a PDA's tool slot installs the
        // // ВорПРО program (only for thieves), taking it out deletes the program again.
        // SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnPdaToolInserted);
        // SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnPdaToolRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiAccumulator += frameTime;
        if (_uiAccumulator < UiRefreshInterval)
            return;

        _uiAccumulator -= UiRefreshInterval;

        var query = AllEntityQuery<ThiefPdaProgramComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Refresh only programs that are currently shown on their PDA.
            if (CompOrNull<CartridgeComponent>(uid)?.LoaderUid is not { } loaderUid ||
                !TryComp<CartridgeLoaderComponent>(loaderUid, out var loader) ||
                loader.ActiveProgram != uid)
            {
                continue;
            }

            UpdateUiState(uid, loaderUid, comp);
        }
    }

    #region Unlock

    /// <summary>
    /// Installs the ВорПРО program when a tool that fits in a utility tool belt is
    /// inserted into the thief's PDA tool slot. Only thieves get the program; the
    /// PDA itself already refuses tool insertion unless it is marked as the thief's own.
    /// </summary>
    private void OnPdaToolInserted(EntInsertedIntoContainerMessage args)
    {
        var pda = args.Container.Owner;

        // Only the thief's marked PDA tool slot triggers the unlock, and it must have a cartridge loader.
        if (args.Container.ID != PdaComponent.PdaToolSlotId ||
            !HasComp<ThiefPdaComponent>(pda) ||
            !TryComp<CartridgeLoaderComponent>(pda, out var loader))
        {
            return;
        }

        var user = GetLoaderUser(pda);
        if (user == null)
            return;

        var mindId = _mind.GetMind(user.Value);
        if (mindId == null || !_roles.MindHasRole<ThiefRoleComponent>(mindId.Value))
            return;

        // Only the round's single randomly-chosen tool unlocks ВорПРО.
        var unlockTool = GetUnlockTool();
        if (unlockTool == null || MetaData(args.Entity).EntityPrototype?.ID != unlockTool)
        {
            _popup.PopupEntity(Loc.GetString("thief-program-wrong-tool"), pda, Filter.Entities(user.Value), true, PopupType.Medium);
            return;
        }

        // Already unlocked.
        foreach (var program in _cartridgeLoader.GetInstalled(pda))
        {
            if (HasComp<ThiefPdaProgramComponent>(program))
                return;
        }

        if (!_cartridgeLoader.InstallProgram(pda, ThiefProgramId, deinstallable: true, loader: loader))
            return;

        _popup.PopupEntity(Loc.GetString("thief-program-unlocked"), pda, Filter.Entities(user.Value), true, PopupType.Medium);
        _audio.PlayPvs(SellSound, pda);
        _adminLogger.Add(LogType.PdaInteract, LogImpact.Medium,
            $"{ToPrettyString(user.Value):actor} unlocked ВорПРО on {ToPrettyString(pda):loader} by inserting {ToPrettyString(args.Entity):tool} into the PDA tool slot");
    }

    /// <summary>
    /// DS14: Returns the entity prototype ID of the round's chosen unlock tool, or null.
    /// </summary>
    private string? GetUnlockTool()
    {
        foreach (var rule in _gameTicker.GetActiveGameRules())
        {
            if (TryComp<GameTicking.Rules.Components.ThiefRuleComponent>(rule, out var ruleComp) &&
                ruleComp.UnlockTool is { } unlockTool)
            {
                return unlockTool;
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes the ВорПРО program when the tool is removed from the PDA's tool slot.
    /// </summary>
    private void OnPdaToolRemoved(EntRemovedFromContainerMessage args)
    {
        var pda = args.Container.Owner;

        if (args.Container.ID != PdaComponent.PdaToolSlotId ||
            !TryComp<CartridgeLoaderComponent>(pda, out var loader))
        {
            return;
        }
        foreach (var program in _cartridgeLoader.GetInstalled(pda))
        {
            if (!HasComp<ThiefPdaProgramComponent>(program))
                continue;

            var user = GetLoaderUser(pda);
            _cartridgeLoader.UninstallProgram(pda, program, loader);

            if (user != null)
            {
                _popup.PopupEntity(Loc.GetString("thief-program-removed"), pda, Filter.Entities(user.Value), true, PopupType.Medium);
                _adminLogger.Add(LogType.PdaInteract, LogImpact.Medium,
                    $"{ToPrettyString(user.Value):actor} removed {ToPrettyString(args.Entity):tool} from {ToPrettyString(pda):loader} and ВорПРО was deleted");
            }

            return;
        }
    }

    #endregion

    #region Ui

    private void OnUiReady(EntityUid uid, ThiefPdaProgramComponent component, CartridgeUiReadyEvent args)
    {
        EnsureOffers(uid, component);
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, ThiefPdaProgramComponent component, CartridgeMessageEvent args)
    {
        if (args is not ThiefProgramUiMessageEvent message)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        var user = args.Actor;

        switch (message.Action)
        {
            case ThiefProgramUiAction.Accept:
                AcceptRequest(uid, loaderUid, user, message.RequestId, component);
                break;
            case ThiefProgramUiAction.Decline:
                DeclineRequest(uid, loaderUid, user, message.RequestId, component);
                break;
            case ThiefProgramUiAction.Sell:
                SellRequest(uid, loaderUid, user, message.RequestId, component);
                break;
            case ThiefProgramUiAction.Buy:
                BuyListing(uid, loaderUid, user, message.ListingId);
                break;
            case ThiefProgramUiAction.Exchange:
                ExchangeCredits(uid, loaderUid, user, message.Amount, component);
                break;
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, ThiefPdaProgramComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new ThiefProgramUiState
        {
            GoalTarget = component.GoalTarget,
        };

        var user = GetLoaderUser(loaderUid);
        if (user != null)
        {
            // The round goal counts only the dCR the thief carries right now,
            // so spending or dropping credits lowers the progress.
            var carried = CountDirtyCredits(user.Value);
            state.Balance = carried;
            state.EarnedTotal = carried;

            var mindId = _mind.GetMind(user.Value);
            if (mindId != null)
                state.BeaconLinked = FindLinkedBeacon(mindId.Value) != null;
        }

        foreach (var offer in component.Offers)
        {
            state.Offers.Add(MakeRequestInfo(offer));
        }

        foreach (var request in component.ActiveRequests)
        {
            state.Active.Add(MakeRequestInfo(request));
        }

        _cartridgeLoader?.UpdateCartridgeUiState(loaderUid, state);
    }

    private ThiefRequestInfo MakeRequestInfo(ThiefProgramRequest request)
    {
        string groupName = request.Group;
        if (_prototype.TryIndex(request.Group, out var groupProto) &&
            _prototype.TryIndex(groupProto.Group, out var stealGroup))
        {
            groupName = Loc.GetString(stealGroup.Name);
        }

        var timeLeft = request.Deadline == TimeSpan.Zero
            ? request.TimeLimitMinutes * 60
            : (int) Math.Max(0, (request.Deadline - _timing.CurTime).TotalSeconds);

        if (timeLeft <= 0 && request.Deadline != TimeSpan.Zero)
            timeLeft = ThiefRequestInfo.Expired;

        return new ThiefRequestInfo(
            request.Id,
            request.Group,
            groupName,
            request.Count,
            request.PricePerItem,
            request.Count * request.PricePerItem,
            request.TimeLimitMinutes,
            timeLeft);
    }

    private EntityUid? GetLoaderUser(EntityUid loaderUid)
    {
        // The PDA is expected to be held by its owner; use the container hierarchy parent.
        var parent = Transform(loaderUid).ParentUid;
        while (parent.IsValid())
        {
            if (HasComp<ActorComponent>(parent))
                return parent;

            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    #endregion

    #region Requests

    private void AcceptRequest(EntityUid uid, EntityUid loaderUid, EntityUid user, int requestId,
        ThiefPdaProgramComponent component)
    {
        if (component.ActiveRequests.Count >= component.MaxActiveRequests)
        {
            NotifyError(loaderUid, user, "thief-program-requests-limit");
            return;
        }

        var offer = component.Offers.Find(r => r.Id == requestId);
        if (offer == null)
            return;

        component.Offers.Remove(offer);
        offer.Deadline = _timing.CurTime + TimeSpan.FromMinutes(offer.TimeLimitMinutes);
        component.ActiveRequests.Add(offer);
        FillOffers(uid, component);
        UpdateUiState(uid, loaderUid, component);
    }

    private void DeclineRequest(EntityUid uid, EntityUid loaderUid, EntityUid user, int requestId,
        ThiefPdaProgramComponent component)
    {
        var request = component.ActiveRequests.Find(r => r.Id == requestId);
        if (request == null)
            return;

        component.ActiveRequests.Remove(request);
        FillOffers(uid, component);
        UpdateUiState(uid, loaderUid, component);
    }

    private void SellRequest(EntityUid uid, EntityUid loaderUid, EntityUid user, int requestId,
        ThiefPdaProgramComponent component)
    {
        var request = component.ActiveRequests.Find(r => r.Id == requestId);
        if (request == null)
            return;

        var mindId = _mind.GetMind(user);
        if (mindId == null)
        {
            NotifyError(loaderUid, user, "thief-program-error-no-mind");
            return;
        }

        var beacon = FindLinkedBeacon(mindId.Value);
        if (beacon == null)
        {
            NotifyError(loaderUid, user, "thief-program-error-no-beacon");
            return;
        }

        var beaconPos = _transform.GetMapCoordinates(beacon.Value);
        var userPos = _transform.GetMapCoordinates(user);
        if (!TryComp<Shared.Objectives.Components.StealAreaComponent>(beacon.Value, out var area) ||
            userPos.MapId != beaconPos.MapId ||
            Vector2.Distance(userPos.Position, beaconPos.Position) > Math.Max(area.Range + 2f, 3f))
        {
            NotifyError(loaderUid, user, "thief-program-error-too-far");
            return;
        }

        var found = new HashSet<Entity<StealTargetComponent>>();
        _lookup.GetEntitiesInRange(beaconPos, Math.Max(area.Range, 1.5f), found, LookupFlags.Dynamic | LookupFlags.Sundries);

        var matches = new List<EntityUid>();
        foreach (var ent in found)
        {
            if (ent.Comp.StealGroup != request.StealGroup)
                continue;

            if (_container.IsEntityInContainer(ent.Owner))
                continue;

            matches.Add(ent.Owner);
        }

        if (matches.Count < request.Count)
        {
            NotifyError(loaderUid, user, "thief-program-error-not-enough");
            return;
        }

        // Consume the items.
        for (var i = 0; i < request.Count; i++)
        {
            Del(matches[i]);
        }

        // Compute the payout with the deadline bonus/penalty.
        var inTime = _timing.CurTime <= request.Deadline;
        var multiplier = inTime ? component.InTimeBonusMultiplier : component.LatePenaltyMultiplier;
        var payout = (int) Math.Round(request.Count * request.PricePerItem * multiplier);

        GiveMoney(user, payout, DirtyCashProto);

        component.ActiveRequests.Remove(request);
        FillOffers(uid, component);

        _audio.PlayPvs(SellSound, beacon.Value);
        var locKey = inTime ? "thief-program-sold-in-time" : "thief-program-sold-late";
        _popup.PopupEntity(Loc.GetString(locKey, ("amount", payout)), loaderUid, Filter.Entities(user), true, PopupType.Medium);
        _adminLogger.Add(LogType.Transactions, LogImpact.Low,
            $"{ToPrettyString(user):actor} sold {request.Count}x {request.Group} at {ToPrettyString(beacon.Value):beacon} for {payout} dirty credits");
    }

    private EntityUid? FindLinkedBeacon(EntityUid mind)
    {
        if (!mind.IsValid())
            return null;

        var query = EntityQueryEnumerator<Shared.Objectives.Components.StealAreaComponent>();
        while (query.MoveNext(out var uid, out var area))
        {
            if (area.Owners.Contains(mind))
                return uid;
        }

        return null;
    }

    private void FillOffers(EntityUid uid, ThiefPdaProgramComponent component)
    {
        while (component.Offers.Count < component.MaxOffers)
        {
            component.Offers.Add(GenerateRequest(component));
        }
    }

    private void EnsureOffers(EntityUid uid, ThiefPdaProgramComponent component)
    {
        FillOffers(uid, component);
    }

    private ThiefProgramRequest GenerateRequest(ThiefPdaProgramComponent component)
    {
        // Never offer the same request type twice at the same time: exclude groups
        // that are already being offered or already accepted.
        var usedGroups = component.Offers
            .Select(o => o.Group)
            .Concat(component.ActiveRequests.Select(r => r.Group))
            .ToHashSet();

        var groups = _prototype.EnumeratePrototypes<ThiefRequestGroupPrototype>()
            .Where(g => !usedGroups.Contains(g.ID))
            .ToList();

        // If every group is currently in use, fall back to any group rather than failing.
        if (groups.Count == 0)
            groups = _prototype.EnumeratePrototypes<ThiefRequestGroupPrototype>().ToList();

        var totalWeight = groups.Sum(g => g.Weight);
        ThiefRequestGroupPrototype picked = groups[0];

        var roll = _random.NextFloat(0f, totalWeight);
        foreach (var group in groups)
        {
            roll -= group.Weight;
            if (roll > 0f)
                continue;

            picked = group;
            break;
        }

        var count = picked.MaxCount > picked.MinCount
            ? _random.Next(picked.MinCount, picked.MaxCount + 1)
            : picked.MinCount;

        return new ThiefProgramRequest
        {
            Id = ++component.NextRequestId,
            Group = picked.ID,
            StealGroup = picked.Group,
            Count = Math.Max(1, count),
            PricePerItem = picked.PricePerItem,
            TimeLimitMinutes = picked.TimeLimitMinutes,
            Deadline = TimeSpan.Zero,
        };
    }

    #endregion

    #region Uplink

    private void BuyListing(EntityUid uid, EntityUid loaderUid, EntityUid user, string listingId)
    {
        if (!_prototype.TryIndex<ThiefListingPrototype>(listingId, out var listing))
        {
            NotifyError(loaderUid, user, "thief-program-uplink-error");
            return;
        }

        var balance = CountDirtyCredits(user);
        if (balance < listing.Cost)
        {
            NotifyError(loaderUid, user, "thief-program-uplink-no-money");
            return;
        }

        TakeDirtyCredits(user, listing.Cost);

        _audio.PlayPvs(BuySound, loaderUid);

        foreach (var proto in listing.Entities)
        {
            var ent = Spawn(proto);
            if (!_hands.TryPickupAnyHand(user, ent))
            {
                _transform.SetCoordinates(ent, Transform(user).Coordinates);
            }
        }

        _adminLogger.Add(LogType.Transactions, LogImpact.Low,
            $"{ToPrettyString(user):actor} bought listing {listingId} from ВорПРО for {listing.Cost} dirty credits");
    }

    private void ExchangeCredits(EntityUid uid, EntityUid loaderUid, EntityUid user, int amount,
        ThiefPdaProgramComponent component)
    {
        if (amount <= 0)
        {
            NotifyError(loaderUid, user, "thief-program-exchange-invalid");
            return;
        }

        var balance = CountDirtyCredits(user);
        if (balance < amount)
        {
            NotifyError(loaderUid, user, "thief-program-exchange-not-enough");
            return;
        }

        TakeDirtyCredits(user, amount);

        // Regular credits are a single unlimited SpaceCash stack, so one entity fits any amount.
        var cash = Spawn(CleanCashProto);
        if (TryComp<StackComponent>(cash, out var cashStack))
            _stack.SetCount(cash, amount, cashStack);

        if (!_hands.TryPickupAnyHand(user, cash))
        {
            _transform.SetCoordinates(cash, Transform(user).Coordinates);
        }

        _popup.PopupEntity(Loc.GetString("thief-program-exchanged", ("amount", amount)), loaderUid,
            Filter.Entities(user), true, PopupType.Medium);
        _adminLogger.Add(LogType.Transactions, LogImpact.Low,
            $"{ToPrettyString(user):actor} laundered {amount} dirty credits into regular credits via ВорПРО");
    }

    #endregion

    #region Dirty credits helpers

    /// <summary>
    /// Recursively collects all entities contained by the holder (inventory, bags inside bags, hands).
    /// </summary>
    private void CollectContainedEntities(EntityUid holder, List<EntityUid> acc)
    {
        if (!TryComp<ContainerManagerComponent>(holder, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                acc.Add(ent);
                CollectContainedEntities(ent, acc);
            }
        }
    }

    public List<Entity<StackComponent>> GetDirtyCreditStacks(EntityUid holder)
    {
        var contained = new List<EntityUid>();
        CollectContainedEntities(holder, contained);

        var stacks = new List<Entity<StackComponent>>();
        foreach (var ent in contained)
        {
            if (TryComp<StackComponent>(ent, out var stack) && stack.StackTypeId == "DirtyCredit")
                stacks.Add((ent, stack));
        }

        return stacks;
    }

    public int CountDirtyCredits(EntityUid holder)
    {
        var total = 0;
        foreach (var stack in GetDirtyCreditStacks(holder))
        {
            total += stack.Comp.Count;
        }

        return total;
    }

    private void TakeDirtyCredits(EntityUid holder, int amount)
    {
        var stacks = new List<(EntityUid Ent, int Count)>();
        foreach (var ent in GetDirtyCreditStacks(holder))
        {
            stacks.Add((ent.Owner, ent.Comp.Count));
        }

        stacks.Sort((a, b) => b.Count.CompareTo(a.Count));

        foreach (var (ent, count) in stacks)
        {
            if (amount <= 0)
                break;

            var take = Math.Min(amount, count);
            _stack.SetCount(ent, count - take);
            amount -= take;
        }
    }

    private void GiveMoney(EntityUid user, int amount, EntProtoId proto)
    {
        var money = Spawn(proto);
        if (TryComp<StackComponent>(money, out var moneyStack))
            _stack.SetCount(money, amount, moneyStack);

        if (!_hands.TryPickupAnyHand(user, money))
        {
            _transform.SetCoordinates(money, Transform(user).Coordinates);
        }
    }

    private void NotifyError(EntityUid loaderUid, EntityUid user, string locKey)
    {
        _audio.PlayPvs(ErrorSound, loaderUid);
        _popup.PopupEntity(Loc.GetString(locKey), loaderUid, Filter.Entities(user), true, PopupType.SmallCaution);
    }

    #endregion
}
