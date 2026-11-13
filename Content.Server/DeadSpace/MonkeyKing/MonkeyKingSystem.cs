// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DeadSpace.MonkeyKing;
using Content.Server.DeadSpace.MonkeyKing.Components;
using Content.Shared.Popups;
using Content.Shared.Hands.Components;
using Robust.Shared.Random;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Mind.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Zombies;

namespace Content.Server.DeadSpace.MonkeyKing;

public sealed partial class MonkeyKingSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MonkeyServantSystem _monkeyServant = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    private static readonly ProtoId<TagPrototype> MonkeyKingTargetTag = "MonkeyKingTarget";
    private static readonly ProtoId<NpcFactionPrototype> NewNpcFaction = "SimpleHostile";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonkeyKingComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MonkeyKingComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<MonkeyKingComponent, ArmyEvent>(OnArmy);
        SubscribeLocalEvent<MonkeyKingComponent, KingBuffActionEvent>(OnKingBuff);
        SubscribeLocalEvent<MonkeyKingComponent, GiveIntelligenceActionEvent>(OnGiveIntelligence);
        SubscribeLocalEvent<MonkeyKingComponent, GiveIntelligenceDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<MonkeyKingComponent, EntityZombifiedEvent>(OnZombified);
    }

    private void OnComponentInit(EntityUid uid, MonkeyKingComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionArmyEntity, component.ActionArmy, uid);
        _actionsSystem.AddAction(uid, ref component.ActionKingBuffEntity, component.ActionKingBuff, uid);
        _actionsSystem.AddAction(uid, ref component.ActionGiveIntelligenceEntity, component.ActionGiveIntelligence, uid);
    }

    private void OnShutdown(EntityUid uid, MonkeyKingComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionArmyEntity);
        _actionsSystem.RemoveAction(uid, component.ActionKingBuffEntity);
        _actionsSystem.RemoveAction(uid, component.ActionGiveIntelligenceEntity);
    }

    private void OnZombified(EntityUid uid, MonkeyKingComponent component, EntityZombifiedEvent args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionArmyEntity);
        _actionsSystem.RemoveAction(uid, component.ActionKingBuffEntity);
        _actionsSystem.RemoveAction(uid, component.ActionGiveIntelligenceEntity);
    }

    private void OnArmy(EntityUid uid, MonkeyKingComponent component, ArmyEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<ZombieComponent>(uid))
            return;

        var monkey = Spawn(component.ServantMonkeyProto, Transform(uid).Coordinates);

        if (component.ArmySound != null)
            _audio.PlayPvs(component.ArmySound, uid, AudioParams.Default.WithVolume(3));

        args.Handled = true;

        if (!TryComp<HandsComponent>(monkey, out var hands))
            return;

        if (hands.Count == 0)
            return;

        var randomIndex = _random.Next(component.WeaponList.Count);
        string randomWeapon = component.WeaponList[randomIndex];

        var weapon = Spawn(randomWeapon, Transform(monkey).Coordinates);

        if (!_hands.TryPickup(monkey, weapon))
            QueueDel(weapon);
    }

    private void OnKingBuff(EntityUid uid, MonkeyKingComponent component, KingBuffActionEvent args)
    {
        if (args.Handled)
            return;

        var entities = _lookup.GetEntitiesInRange<MonkeyServantComponent>(_transform.GetMapCoordinates(uid, Transform(uid)), component.RangeBuff);

        if (entities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("monkey-no-one-to-buff"), uid, uid);
            return;
        }

        foreach (var entity in entities)
        {
            if (!_mobState.IsAlive(entity))
                continue;

            _monkeyServant.Buff(entity, entity.Comp, TimeSpan.FromSeconds(component.BuffDuration), component.SpeedBuff, component.GetDamageBuff);
        }

        if (component.KingBuffSound != null)
            _audio.PlayPvs(component.KingBuffSound, uid, AudioParams.Default.WithVolume(3));

        args.Handled = true;
    }

    private void OnGiveIntelligence(EntityUid uid, MonkeyKingComponent component, GiveIntelligenceActionEvent args)
    {
        if (args.Handled || args.Target == uid)
            return;

        var target = args.Target;

        if (!_tagSystem.HasTag(target, MonkeyKingTargetTag))
        {
            _popup.PopupEntity(Loc.GetString("dont-give-intelligence"), uid, uid);
            return;
        }

        var searchDoAfter = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(component.GiveIntelligenceDuration), new GiveIntelligenceDoAfterEvent(), uid, target: target)
        {
            DistanceThreshold = 2
        };

        if (!_doAfter.TryStartDoAfter(searchDoAfter))
            return;


        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, MonkeyKingComponent component, GiveIntelligenceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;

        RemComp<ReplacementAccentComponent>(target);
        RemComp<MonkeyAccentComponent>(target);

        if (TryComp<MindContainerComponent>(target, out var mindContainer) && mindContainer.HasMind)
            return;

        if (TryComp(target, out GhostRoleComponent? ghostRole))
            return;

        ghostRole = AddComp<GhostRoleComponent>(target);
        EnsureComp<GhostTakeoverAvailableComponent>(target);

        EnsureComp<MonkeyServantComponent>(target);

        if (TryComp(target, out MetaDataComponent? entityData))
        {
            ghostRole.RoleName = entityData.EntityName;
            ghostRole.RoleDescription = Loc.GetString("ghost-role-information-intelligence-description");
        }

        _faction.ClearFactions(target, dirty: false);
        _faction.AddFaction(target, NewNpcFaction);

        if (component.GiveIntelligenceSound != null)
            _audio.PlayPvs(component.GiveIntelligenceSound, uid, AudioParams.Default.WithVolume(3));
    }
}
