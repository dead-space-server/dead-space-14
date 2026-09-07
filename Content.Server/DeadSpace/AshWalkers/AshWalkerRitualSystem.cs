using System.Linq;
using Content.Server.EUI;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerRitualSystem : SharedAshWalkerRitualSystem
{
    private const float OfferingRange = 1.5f;
    private static readonly string[] HealingTypes = ["Blunt", "Slash", "Piercing", "Heat", "Cold", "Caustic"];

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly MovementModStatusSystem _movementStatus = default!;
    [Dependency] private readonly OpenableSystem _openable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    private readonly Dictionary<EntityUid, AshWalkerReturnEui> _pendingReturns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshWalkerRuneComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<AshWalkerRuneComponent, DragDropTargetEvent>(OnDrop);
        SubscribeLocalEvent<AshWalkerRuneComponent, GetVerbsEvent<InteractionVerb>>(OnInvokeVerb);
        SubscribeLocalEvent<AshWalkerRuneComponent, AshWalkerRitualDoAfterEvent>(OnRitual);
        SubscribeLocalEvent<AshWalkerRuneComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnActivate(Entity<AshWalkerRuneComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!args.Handled)
            args.Handled = TryBeginRitual(ent, args.User);
    }

    private void OnDrop(Entity<AshWalkerRuneComponent> ent, ref DragDropTargetEvent args)
    {
        if (!args.Handled)
            args.Handled = TryBeginRitual(ent, args.User, args.Dragged);
    }

    private void OnInvokeVerb(Entity<AshWalkerRuneComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanUseRune(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("ash-walker-rune-invoke"),
            Act = () => TryBeginRitual(ent, user),
        });
    }

    public bool TryBeginRitual(Entity<AshWalkerRuneComponent> rune, EntityUid user, EntityUid? preferred = null)
    {
        if (!CanUseRune(rune, user) || _doAfter.IsRunning(rune.Comp.Invocation) || _pendingReturns.ContainsKey(rune))
            return false;

        var nearby = _lookup.GetEntitiesInRange(Transform(rune).Coordinates, OfferingRange);
        if (preferred is { } target && IsNearby(rune, target))
            nearby.Add(target);

        var request = new AshWalkerRitualDoAfterEvent();
        var sacrifices = nearby.Where(uid => IsSacrifice(rune, uid)).OrderBy(uid => uid == preferred ? 0 : 1)
            .ThenBy(uid => uid).Take(rune.Comp.SacrificeCount).ToList();
        if (sacrifices.Count != rune.Comp.SacrificeCount)
            return Fail(rune, user, "ash-walker-rune-missing-offering");

        request.Sacrifices = sacrifices.Select(uid => GetNetEntity(uid)).ToList();
        if (rune.Comp.Rite == AshWalkerRite.Return)
        {
            var subject = nearby.Where(uid => IsReturnSubject(rune, uid, out _))
                .OrderBy(uid => uid == preferred ? 0 : 1).ThenBy(uid => uid).Cast<EntityUid?>().FirstOrDefault();
            if (subject == null || !IsReturnSubject(rune, subject.Value, out var mind))
                return Fail(rune, user, "ash-walker-rune-no-spirit");
            if (!TryGetTonic(rune, user, out _))
                return Fail(rune, user, "ash-walker-rune-missing-tonic");

            request.Subject = GetNetEntity(subject.Value);
            request.Mind = GetNetEntity(mind);
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, rune.Comp.RitualTime, request, rune, target: rune)
            { BreakOnMove = true, BreakOnDamage = true, NeedHand = true }, out rune.Comp.Invocation);
    }

    private void OnRitual(Entity<AshWalkerRuneComponent> ent, ref AshWalkerRitualDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Invocation != args.DoAfter.Id)
            return;

        args.Handled = true;
        ent.Comp.Invocation = null;
        if (args.Cancelled || !ValidateOfferings(ent, args.User, args))
            return;

        if (ent.Comp.Rite == AshWalkerRite.Return)
        {
            if (args.Subject is not { } subject || args.Mind is not { } mind ||
                !IsReturnSubject(ent, GetEntity(subject), out var currentMind) || currentMind != GetEntity(mind) ||
                !_players.TryGetSessionById(Comp<MindComponent>(currentMind).UserId, out var session) ||
                !TryGetTonic(ent, args.User, out _))
                return;

            var request = new AshWalkerReturnEui(this, ent, args.User, args, _timing.CurTime + TimeSpan.FromSeconds(30));
            _pendingReturns.Add(ent, request);
            _eui.OpenEui(request, session);
            _popup.PopupEntity(Loc.GetString("ash-walker-rune-awaiting-spirit"), ent, args.User);
            return;
        }

        if (ent.Comp.Effect is not { } effect)
            return;

        var applied = false;
        foreach (var recipient in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 2f))
        {
            if (!_mobState.IsAlive(recipient) || !HasComp<AshWalkerComponent>(recipient) ||
                !TryComp<AshWalkerTribeMemberComponent>(recipient, out var tribe) || tribe.HomeMap != ent.Comp.HomeMap ||
                !_interaction.InRangeUnobstructed(ent.Owner, recipient, range: 2f) ||
                _status.HasStatusEffect(recipient, effect))
                continue;

            applied |= _status.TrySetStatusEffectDuration(recipient, effect, ent.Comp.EffectDuration);
        }

        if (applied)
            ConsumeOfferings(ent, args.User, args);
        else
            Fail(ent, args.User, "ash-walker-rune-already-blessed");
    }

    private bool ValidateOfferings(Entity<AshWalkerRuneComponent> rune, EntityUid user, AshWalkerRitualDoAfterEvent request)
    {
        return CanUseRune(rune, user) && request.Sacrifices.Count == rune.Comp.SacrificeCount &&
               request.Sacrifices.Distinct().Count() == request.Sacrifices.Count &&
               request.Sacrifices.All(uid => IsSacrifice(rune, GetEntity(uid)));
    }

    private bool IsNearby(EntityUid rune, EntityUid target)
    {
        return !TerminatingOrDeleted(target) && !EntityManager.IsQueuedForDeletion(target) &&
               !_containers.IsEntityInContainer(target) &&
               _interaction.InRangeUnobstructed(rune, target, range: OfferingRange);
    }

    private bool IsSacrifice(Entity<AshWalkerRuneComponent> rune, EntityUid target)
    {
        return IsNearby(rune, target) && MetaData(target).EntityPrototype is { } prototype &&
               rune.Comp.Sacrifices.Contains(prototype.ID) && !HasComp<AshWalkerComponent>(target) &&
               !HasComp<GutlunchComponent>(target) &&
               (IsValidOffering(target) || _mobState.IsDead(target) && HasComp<ButcherableComponent>(target));
    }

    private bool IsReturnSubject(Entity<AshWalkerRuneComponent> rune, EntityUid subject, out EntityUid mind)
    {
        mind = default;
        return IsNearby(rune, subject) && HasComp<AshWalkerComponent>(subject) && _mobState.IsDead(subject) &&
               TryComp<AshWalkerTribeMemberComponent>(subject, out var tribe) && tribe.HomeMap == rune.Comp.HomeMap &&
               HasComp<BloodstreamComponent>(subject) && HasComp<DamageableComponent>(subject) &&
               _body.GetBodyChildrenOfType(subject, BodyPartType.Head).Any() &&
               _mind.TryGetMind(subject, out mind, out var mindComp) && mindComp.OwnedEntity == subject &&
               !HasComp<AshWalkerReturnedComponent>(mind) && _players.TryGetSessionById(mindComp.UserId, out _);
    }

    private bool TryGetTonic(EntityUid rune, EntityUid user, out Entity<SolutionComponent> tonic)
    {
        tonic = default;
        var candidates = _lookup.GetEntitiesInRange(Transform(rune).Coordinates, OfferingRange);
        foreach (var held in _hands.EnumerateHeld(user))
            candidates.Add(held);

        foreach (var candidate in candidates)
        {
            if ((!IsNearby(rune, candidate) && !_hands.IsHolding(user, candidate)) ||
                _openable.IsClosed(candidate) ||
                !_solutions.TryGetDrainableSolution(candidate, out var solution, out var contents) ||
                contents.GetTotalPrototypeQuantity("AshWalkerBloodTonic") < FixedPoint2.New(10))
                continue;

            tonic = solution.Value;
            return true;
        }

        return false;
    }

    public void ConfirmReturn(AshWalkerReturnEui prompt, bool accepted)
    {
        if (!_pendingReturns.TryGetValue(prompt.Rune, out var current) || current != prompt)
            return;

        _pendingReturns.Remove(prompt.Rune);

        if (!TryComp<AshWalkerRuneComponent>(prompt.Rune, out var rune))
            return;

        var request = prompt.Request;
        if (!accepted || _timing.CurTime > prompt.ExpiresAt || !ValidateOfferings((prompt.Rune, rune), prompt.Invoker, request) ||
            request.Subject is not { } subjectNet || request.Mind is not { } mindNet ||
            !IsReturnSubject((prompt.Rune, rune), GetEntity(subjectNet), out var mind) || mind != GetEntity(mindNet) ||
            Comp<MindComponent>(mind).UserId != prompt.Player.UserId ||
            !TryGetTonic(prompt.Rune, prompt.Invoker, out var tonic))
            return;

        var subject = GetEntity(subjectNet);
        if (!_thresholds.TryGetThresholdForState(subject, MobState.Critical, out var critical))
            return;

        EnsureComp<AshWalkerReturnedComponent>(mind);
        _solutions.RemoveReagent(tonic, "AshWalkerBloodTonic", FixedPoint2.New(10));
        ConsumeOfferings((prompt.Rune, rune), prompt.Invoker, request);
        var wounds = new DamageSpecifier();
        wounds.DamageDict["Blunt"] = critical.Value * 0.5f;
        _damage.SetDamage(subject, wounds);
        _blood.TryModifyBleedAmount(subject, -100);
        _blood.TryModifyBloodLevel(subject, 100);
        _mobState.ChangeMobState(subject, MobState.Alive);
        _movementStatus.TryUpdateMovementSpeedModDuration(subject, "AshWalkerReturnWeaknessEffect", TimeSpan.FromSeconds(60), 0.7f);
        _mind.UnVisit(prompt.Player);
        _popup.PopupEntity(Loc.GetString("ash-walker-rune-returned"), subject, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(prompt.Invoker):user} returned {ToPrettyString(subject):subject} with {ToPrettyString(prompt.Rune):rune}.");
    }

    private void ConsumeOfferings(Entity<AshWalkerRuneComponent> rune, EntityUid user, AshWalkerRitualDoAfterEvent request)
    {
        foreach (var net in request.Sacrifices)
        {
            var offering = GetEntity(net);
            RemComp<ButcherableComponent>(offering);
            RemComp<AshWalkerOfferingComponent>(offering);
            if (_inventory.TryGetSlots(offering, out var slots))
            {
                foreach (var slot in slots)
                    _inventory.TryUnequip(offering, slot.Name, silent: true, force: true);
            }
            foreach (var item in _inventory.GetHandOrInventoryEntities((offering, null, null)).ToArray())
            {
                _containers.TryRemoveFromContainer(item, force: true);
                _transform.DropNextTo(item, offering);
            }
            _adminLog.Add(LogType.Gib, LogImpact.Medium,
                $"{ToPrettyString(user):user} offered {ToPrettyString(offering):offering} to {ToPrettyString(rune):rune}.");
            QueueDel(offering);
        }
        _audio.PlayPvs(new SoundCollectionSpecifier("gib"), rune);
    }

    private bool Fail(EntityUid rune, EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), rune, user);
        return false;
    }

    private void OnShutdown(Entity<AshWalkerRuneComponent> ent, ref ComponentShutdown args)
    {
        if (_pendingReturns.Remove(ent, out var prompt))
            prompt.Close();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var prompt in _pendingReturns.Values.ToArray())
        {
            if (_timing.CurTime <= prompt.ExpiresAt)
                continue;
            ConfirmReturn(prompt, false);
            prompt.Close();
        }

        var query = EntityQueryEnumerator<AshWalkerRitualEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var effect, out var status))
        {
            if (effect.HealingBudget <= FixedPoint2.Zero || !status.Applied || status.AppliedTo is not { } subject ||
                !_mobState.IsAlive(subject) || !TryComp<DamageableComponent>(subject, out var damage))
                continue;

            effect.Elapsed += frameTime;
            if (effect.Elapsed < effect.HealingInterval.TotalSeconds)
                continue;
            effect.Elapsed = 0;
            var remaining = FixedPoint2.Min(effect.HealingBudget, effect.HealingPerTick);
            var healing = new DamageSpecifier();
            foreach (var type in HealingTypes)
            {
                var amount = FixedPoint2.Min(remaining, damage.Damage.DamageDict.GetValueOrDefault(type));
                if (amount <= FixedPoint2.Zero)
                    continue;
                healing.DamageDict[type] = -amount;
                remaining -= amount;
            }
            if (_damage.TryChangeDamage(subject, healing, out var healed, ignoreResistances: true,
                    interruptsDoAfters: false, ignoreGlobalModifiers: true))
                effect.HealingBudget -= FixedPoint2.Max(FixedPoint2.Zero, -healed.GetTotal());
        }
    }
}
