// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.ChromiteCaves;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Abilities.Goliath;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Creatures brought back by a lazarus injector serve whoever revived them: while any of them is alive the tamer
/// has the rat king's orders - follow, stay, attack what I point at. What the tamed creatures spawn
/// (hivelord broods, legion heads, broodmother children) fights on the tamer's side as well, but takes no orders.
/// </summary>
public sealed class LavalandTamingSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId FollowAction = "ActionLavalandTamedFollow";
    private static readonly EntProtoId StayAction = "ActionLavalandTamedStay";
    private static readonly EntProtoId AttackAction = "ActionLavalandTamedAttack";

    private const string FollowCompound = "LavalandTamedFollowCompound";
    private const string AttackCompound = "LavalandTamedAttackCompound";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private const float ShootRange = 7f;
    private const float BiteRange = 1.3f;

    private readonly List<EntityUid> _gone = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandTamerComponent, LavalandTamedOrderActionEvent>(OnOrder);
        SubscribeLocalEvent<LavalandTamerComponent, AfterPointedAtEvent>(OnPointedAt);
        SubscribeLocalEvent<LavalandTamerComponent, ComponentShutdown>(OnTamerShutdown);
        SubscribeLocalEvent<LavalandTamedComponent, GunShotEvent>(OnTamedShot);
        SubscribeLocalEvent<LavalandTamedComponent, MobStateChangedEvent>(OnTamedStateChanged);
        SubscribeLocalEvent<LavalandTamedComponent, EmpPulseEvent>(OnTamedEmp);
        SubscribeLocalEvent<LavalandTamedComponent, GoliathSummonTentacleAction>(OnTamedTentacles);
    }

    #region Public

    /// <summary>The creature now serves the tamer and fights for these factions.</summary>
    public void Tame(EntityUid mob, EntityUid tamer, IEnumerable<ProtoId<NpcFactionPrototype>> factions)
    {
        var fresh = !HasComp<LavalandTamedComponent>(mob);
        var tamed = EnsureComp<LavalandTamedComponent>(mob);
        if (tamed.Tamer is { } old && old != tamer && TryComp<LavalandTamerComponent>(old, out var oldTamer))
            oldTamer.Tamed.Remove(mob);

        // What it was, for an EMP to turn it back.
        if (fresh)
        {
            tamed.OriginalFactions = TryComp<NpcFactionMemberComponent>(mob, out var member)
                ? member.Factions.ToList()
                : new List<ProtoId<NpcFactionPrototype>>();
            tamed.OriginalRoot = TryComp<HTNComponent>(mob, out var originalHtn) ? originalHtn.RootTask.Task : null;
        }

        tamed.Tamer = tamer;
        tamed.Target = null;
        tamed.Factions = factions.ToList();
        _faction.ClearFactions(mob, false);
        _faction.AddFactions(mob, tamed.Factions.ToHashSet());

        // A chromite caves argocyte stops hunting through the chasms on its own: it only obeys now.
        RemComp<LavalandPitJumperAlertComponent>(mob);
        RemComp<LavalandPitJumpingComponent>(mob);
        RemComp<LavalandPitJumperComponent>(mob);

        var comp = EnsureComp<LavalandTamerComponent>(tamer);
        if (!comp.Tamed.Contains(mob))
            comp.Tamed.Add(mob);

        EnsureActions(tamer, comp);
        ApplyOrder(mob, tamer, comp.Order);
    }

    /// <summary>
    /// Something spawned by a tamed creature (not through its gun - that is handled here) joins its side;
    /// it is not one of the tamer's units and takes no orders.
    /// </summary>
    public void InheritSide(EntityUid parent, EntityUid child)
    {
        if (!TryComp<LavalandTamedComponent>(parent, out var tamed) || tamed.Factions.Count == 0 ||
            !HasComp<NpcFactionMemberComponent>(child))
        {
            return;
        }

        _faction.ClearFactions(child, false);
        _faction.AddFactions(child, tamed.Factions.ToHashSet());
    }

    /// <summary>It is wild again: its old side and its own AI, no longer under command.</summary>
    public void Untame(Entity<LavalandTamedComponent> ent)
    {
        if (ent.Comp.Tamer is { } tamer && TryComp<LavalandTamerComponent>(tamer, out var tamerComp))
        {
            tamerComp.Tamed.Remove(ent);
            tamerComp.NextCheck = TimeSpan.Zero;
        }

        _faction.ClearFactions(ent.Owner, false);
        _faction.AddFactions(ent.Owner, ent.Comp.OriginalFactions.ToHashSet());

        if (TryComp<HTNComponent>(ent, out var htn))
        {
            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);
            if (ent.Comp.OriginalRoot is { } root)
                htn.RootTask = new HTNCompoundTask { Task = root };
            _htn.SetHTNEnabled((ent, htn), true);
            _htn.Replan(htn);
        }

        RemComp<LavalandTamedComponent>(ent);
    }

    #endregion

    /// <summary>The tentacles come up after their spawn effect and stay a moment: the tamer is safe from them that long.</summary>
    private static readonly TimeSpan TentacleSafeTime = TimeSpan.FromSeconds(1.5);

    /// <summary>A tamed goliath's tentacles do not knock its own tamer down.</summary>
    private void OnTamedTentacles(Entity<LavalandTamedComponent> ent, ref GoliathSummonTentacleAction args)
    {
        if (ent.Comp.Tamer is not { } tamer || TerminatingOrDeleted(tamer))
            return;

        EnsureComp<LavalandTentacleSafeComponent>(tamer).Until = _timing.CurTime + TentacleSafeTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateAttacks();

        var now = _timing.CurTime;
        var safeQuery = EntityQueryEnumerator<LavalandTentacleSafeComponent>();
        while (safeQuery.MoveNext(out var safeUid, out var safe))
        {
            if (now >= safe.Until)
                RemCompDeferred<LavalandTentacleSafeComponent>(safeUid);
        }

        var query = EntityQueryEnumerator<LavalandTamerComponent>();
        while (query.MoveNext(out var uid, out var tamer))
        {
            if (now < tamer.NextCheck)
                continue;

            tamer.NextCheck = now + CheckInterval;
            Prune(uid, tamer);
        }
    }

    /// <summary>The dead and the gone are no longer under command; with nobody left the orders go away.</summary>
    private void Prune(EntityUid uid, LavalandTamerComponent tamer)
    {
        tamer.Tamed.RemoveAll(mob => TerminatingOrDeleted(mob) || _mobState.IsDead(mob) ||
                                      !TryComp<LavalandTamedComponent>(mob, out var tamed) || tamed.Tamer != uid);

        if (tamer.Tamed.Count == 0)
            RemCompDeferred<LavalandTamerComponent>(uid);
    }

    #region Orders

    private void EnsureActions(EntityUid uid, LavalandTamerComponent comp)
    {
        if (comp.FollowAction == null)
            _actions.AddAction(uid, ref comp.FollowAction, FollowAction);
        if (comp.StayAction == null)
            _actions.AddAction(uid, ref comp.StayAction, StayAction);
        if (comp.AttackAction == null)
            _actions.AddAction(uid, ref comp.AttackAction, AttackAction);

        UpdateToggles(comp);
    }

    private void UpdateToggles(LavalandTamerComponent comp)
    {
        _actions.SetToggled(comp.FollowAction, comp.Order == LavalandTamedOrder.Follow);
        _actions.SetToggled(comp.StayAction, comp.Order == LavalandTamedOrder.Stay);
        _actions.SetToggled(comp.AttackAction, comp.Order == LavalandTamedOrder.Attack);
    }

    private void OnTamerShutdown(Entity<LavalandTamerComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.FollowAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.StayAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.AttackAction);
    }

    private void OnOrder(Entity<LavalandTamerComponent> ent, ref LavalandTamedOrderActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Order = args.Order;
        UpdateToggles(ent.Comp);

        Prune(ent, ent.Comp);
        foreach (var mob in ent.Comp.Tamed)
        {
            ApplyOrder(mob, ent, args.Order);
        }

        _popup.PopupEntity(Loc.GetString($"lavaland-tamed-order-{args.Order.ToString().ToLowerInvariant()}"), ent, ent);
    }

    private void ApplyOrder(EntityUid mob, EntityUid tamer, LavalandTamedOrder order)
    {
        if (!TryComp<HTNComponent>(mob, out var htn))
            return;

        _npc.SetBlackboard(mob, NPCBlackboard.FollowTarget, new EntityCoordinates(tamer, Vector2.Zero));

        if (TryComp<LavalandTamedComponent>(mob, out var tamed) && order != LavalandTamedOrder.Attack)
            tamed.Target = null;

        // Stay: it stands where it is and does nothing until told otherwise.
        if (order == LavalandTamedOrder.Stay)
        {
            _htn.SetHTNEnabled((mob, htn), false);
            return;
        }

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        htn.RootTask = new HTNCompoundTask { Task = order == LavalandTamedOrder.Attack ? AttackCompound : FollowCompound };
        _htn.SetHTNEnabled((mob, htn), true);
        _htn.Replan(htn);
    }

    /// <summary>Attack order: whatever the tamer points at becomes everyone's target.</summary>
    private void OnPointedAt(Entity<LavalandTamerComponent> ent, ref AfterPointedAtEvent args)
    {
        if (ent.Comp.Order != LavalandTamedOrder.Attack)
            return;

        var target = args.Pointed;
        if (target == ent.Owner || !HasComp<MobStateComponent>(target) ||
            TryComp<LavalandTamedComponent>(target, out var tamedTarget) && tamedTarget.Tamer == ent.Owner)
        {
            return;
        }

        foreach (var mob in ent.Comp.Tamed)
        {
            if (TerminatingOrDeleted(mob) || _mobState.IsDead(mob) || !TryComp<LavalandTamedComponent>(mob, out var tamed))
                continue;

            _faction.AggroEntity(mob, target);
            tamed.Target = target;

            // It goes for it by itself (see UpdateAttacks): its AI waits meanwhile, but it must stay awake to move.
            if (TryComp<HTNComponent>(mob, out var htn))
                _htn.SetHTNEnabled((mob, htn), false);
            if (!HasComp<ActiveNPCComponent>(mob))
                _npc.WakeNPC(mob);
        }
    }

    /// <summary>Everyone with a pointed-at target runs at it and attacks; done with it, back to the standing order.</summary>
    private void UpdateAttacks()
    {
        var query = EntityQueryEnumerator<LavalandTamedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tamed, out var xform))
        {
            if (tamed.Target is not { } target)
                continue;

            var tamer = tamed.Tamer;
            var order = tamer != null && TryComp<LavalandTamerComponent>(tamer, out var tamerComp)
                ? tamerComp.Order
                : LavalandTamedOrder.Follow;

            if (_mobState.IsDead(uid) || order != LavalandTamedOrder.Attack ||
                TerminatingOrDeleted(target) || !_mobState.IsAlive(target) || Transform(target).MapID != xform.MapID)
            {
                tamed.Target = null;
                if (tamer is { } owner && !TerminatingOrDeleted(owner) && !_mobState.IsDead(uid))
                    ApplyOrder(uid, owner, order);
                continue;
            }

            AttackTarget(uid, xform, target);
        }
    }

    private void AttackTarget(EntityUid uid, TransformComponent xform, EntityUid target)
    {
        var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
        var distance = offset.Length();

        // Something that shoots: from range, if it can see the target.
        if (TryComp<GunComponent>(uid, out var gun) && distance <= ShootRange &&
            _interaction.InRangeUnobstructed(uid, target, ShootRange))
        {
            _gun.AttemptShoot(uid, (uid, gun), Transform(target).Coordinates, target);
            return;
        }

        if (distance > BiteRange && TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CurTickSprintMovement = offset / distance;
            mover.LastInputTick = _timing.CurTick;
            mover.LastInputSubTick = ushort.MaxValue;
        }

        // Strikes as soon as the target is within its reach - a big creature stops farther off than the bite range.
        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) &&
            distance <= MathF.Max(BiteRange, weapon.Range) && weapon.NextAttack <= _timing.CurTime)
        {
            // A blow needs combat mode on; with its AI off nothing else turns it on.
            _combatMode.SetInCombatMode(uid, true);
            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);
        }
    }

    /// <summary>An EMP scrambles the nanomachines: the creature is wild again.</summary>
    private void OnTamedEmp(Entity<LavalandTamedComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        if (ent.Comp.Tamer is { } tamer && !TerminatingOrDeleted(tamer))
            _popup.PopupEntity(Loc.GetString("lavaland-tamed-emp", ("target", ent.Owner)), ent, tamer, PopupType.MediumCaution);
        Untame(ent);
    }

    #endregion

    /// <summary>
    /// A hivelord's brood, a legion's head: what a tamed creature shoots out is on its side.
    /// (Not <see cref="AmmoShotEvent"/>: a creature without a projectile component is thrown, not shot, and is left out of it.)
    /// </summary>
    private void OnTamedShot(Entity<LavalandTamedComponent> ent, ref GunShotEvent args)
    {
        foreach (var (ammo, _) in args.Ammo)
        {
            if (ammo is not { } projectile || TerminatingOrDeleted(projectile))
                continue;

            InheritSide(ent, projectile);
            if (ent.Comp.Tamer is { } tamer && HasComp<HTNComponent>(projectile))
                _npc.SetBlackboard(projectile, NPCBlackboard.FollowTarget, new EntityCoordinates(tamer, Vector2.Zero));
        }
    }

    private void OnTamedStateChanged(Entity<LavalandTamedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && ent.Comp.Tamer is { } tamer && TryComp<LavalandTamerComponent>(tamer, out var comp))
            comp.NextCheck = TimeSpan.Zero;
    }
}
