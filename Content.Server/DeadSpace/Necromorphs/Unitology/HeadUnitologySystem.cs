// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Necromorphs.Unitology;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Server.Popups;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Zombies;
using Content.Server.DeadSpace.Necromorphs.InfectionDead;
using Content.Shared.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using System.Linq;
using Content.Server.Mind;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Shared.Chat;
using Content.Shared.Maps;
using Content.Shared.Decals;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mindshield.Components;
using Content.Server.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Content.Server.Hands.Systems;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

public sealed class UnitologyHeadSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly InfectionDeadSystem _infectionDead = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UnitologyRuleSystem _unitologyRule = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly SuperNecroobeliskSystem _superObelisk = default!;


    public const float DistanceRecruitmentDetermination = 2f;
    public const string CandleTag = "Candle";
    private static readonly HashSet<string> HeadImplants =
    [
        "StorageImplant",
        "DnaScramblerImplant",
        "FreedomImplant",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnitologyHeadComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<UnitologyHeadComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<UnitologyHeadComponent, UnitologyHeadActionEvent>(OnHeadUnitology);
        SubscribeLocalEvent<UnitologyHeadComponent, OrderToSlaveActionEvent>(OnOrder);
        SubscribeLocalEvent<UnitologyHeadComponent, SelectTargetRecruitmentEvent>(OnSelectTargetRecruitment);
        SubscribeLocalEvent<UnitologyHeadComponent, UnitologistRecruitmentDoAfterEvent>(OnRecruitmentDoAfter);
        SubscribeLocalEvent<UnitologyHeadComponent, ObeliskActionEvent>(OnSummonObelisk);
        SubscribeLocalEvent<UnitologyHeadComponent, SummonUnitologyObeliskDoAfterEvent>(OnSummonObeliskDoAfter);
    }

    private void OnComponentInit(EntityUid uid, UnitologyHeadComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionUnitologyHeadEntity, component.ActionUnitologyHead, uid);
        _actionsSystem.AddAction(uid, ref component.ActionOrderToSlaveEntity, component.ActionOrderToSlave, uid);
        _actionsSystem.AddAction(uid, ref component.ActionSummonObeliskEntity, component.ActionSummonObelisk, uid);
    }

    private void OnShutDown(EntityUid uid, UnitologyHeadComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionUnitologyHeadEntity);
        _actionsSystem.RemoveAction(uid, component.ActionOrderToSlaveEntity);
        _actionsSystem.RemoveAction(uid, component.ActionSelectTargetRecruitmentEntity);
        _actionsSystem.RemoveAction(uid, component.ActionSummonObeliskEntity);
    }

    private void OnSummonObelisk(EntityUid uid, UnitologyHeadComponent component, ObeliskActionEvent args)
    {
        if (args.Handled)
            return;

        // The Circle's geist on its own instead lets a supermatter obelisk nearby reach 100% in any mode.
        if (TryComp<TheCircleGeistSaboteurComponent>(uid, out var saboteur) &&
            TryGetSuperObelisk(uid, saboteur.SuperObeliskRange, out var superObelisk))
        {
            var superDoAfter = new DoAfterArgs(EntityManager,
                uid,
                TimeSpan.FromSeconds(15),
                new SummonUnitologyObeliskDoAfterEvent(),
                uid,
                target: superObelisk)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BlockDuplicate = true,
                CancelDuplicate = true,
            };

            if (_doAfter.TryStartDoAfter(superDoAfter))
                args.Handled = true;
            return;
        }

        if (!_unitologyRule.AreSummoningConditionsComplete(uid))
        {
            _popup.PopupEntity(Loc.GetString("unitology-obelisk-summon-conditions-failed"), uid, uid);
            return;
        }

        if (!_hands.TryGetActiveItem(uid, out var splinter) ||
            !TryComp<NecroobeliskSplinterComponent>(splinter, out var heldSplinter) ||
            !heldSplinter.CanSummonObelisk)
        {
            _popup.PopupEntity(Loc.GetString("unitology-obelisk-summon-no-splinter"), uid, uid);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            uid,
            TimeSpan.FromSeconds(15),
            new SummonUnitologyObeliskDoAfterEvent(),
            uid,
            used: splinter)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnSummonObeliskDoAfter(EntityUid uid,
        UnitologyHeadComponent component,
        SummonUnitologyObeliskDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Args.Target is { } target &&
            TryComp<SuperMatterialNecroObeliskComponent>(target, out var superObelisk))
        {
            _superObelisk.LiftActivationCap(target, superObelisk);
            args.Handled = true;
            return;
        }

        if (args.Args.Used is not { } splinter)
            return;

        if (!_unitologyRule.AreSummoningConditionsComplete(uid) ||
            !_hands.TryGetActiveItem(uid, out var activeItem) ||
            activeItem != splinter ||
            !TryComp<NecroobeliskSplinterComponent>(splinter, out var splinterComponent) ||
            !splinterComponent.CanSummonObelisk)
        {
            _popup.PopupEntity(Loc.GetString("unitology-obelisk-summon-interrupted"), uid, uid);
            return;
        }

        if (!_unitologyRule.TrySummonObelisk(uid, splinterComponent.SpawnsBlackObelisk))
            return;

        QueueDel(splinter);
        args.Handled = true;
    }

    private bool TryGetSuperObelisk(EntityUid uid, float range, out EntityUid obelisk)
    {
        obelisk = default;
        foreach (var candidate in _lookup.GetEntitiesInRange<SuperMatterialNecroObeliskComponent>(Transform(uid).Coordinates, range))
        {
            if (candidate.Comp.StateEnum == SuperMatterialNecroObeliskState.Hundred)
                continue;

            obelisk = candidate;
            return true;
        }

        return false;
    }

    private void OnSelectTargetRecruitment(EntityUid uid, UnitologyHeadComponent component, SelectTargetRecruitmentEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;

        if (!_infectionDead.IsInfectionPossible(target)
        || !HasComp<HumanoidAppearanceComponent>(target)
        || HasComp<MindShieldComponent>(target)
        || HasComp<UnitologyHeadComponent>(target)
        || HasComp<UnitologyComponent>(target)
        || HasComp<UnitologyEnslavedComponent>(target)
        || !_mobState.IsAlive(target)
        || !_mindSystem.TryGetMind(target, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("Цель не подходит для вербовки."), uid, uid);
            return;
        }

        var entities = _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid, Transform(uid)), DistanceRecruitmentDetermination).ToList();
        var candlesEntities = entities
            .Where(ent => ent != uid && _tags.HasTag(ent, CandleTag))
            .ToList();

        var xform = Transform(target);
        var tileref = _turf.GetTileRef(xform.Coordinates);

        if (tileref == null)
            return;

        var decals = _decals.GetDecalsInRange(tileref.Value.GridUid, _turf.GetTileCenter(tileref.Value).Position, 1f);

        var penctagramDecals = _prototypeManager.EnumeratePrototypes<DecalPrototype>()
        .Where(x => x.Tags.Contains("uni-penctagram"))
        .Select(x => x.ID)
        .ToArray();

        bool condition = false;

        foreach (var (id, decal) in decals)
        {
            if (penctagramDecals.Contains(decal.Id))
            {
                condition = true;
                break;
            }
        }

        if (candlesEntities.Count() < component.NumberOfCandles || !condition)
        {
            _popup.PopupEntity(Loc.GetString("Условия не выполнены!"), uid, uid);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, component.VerbDuration, new UnitologistRecruitmentDoAfterEvent(), uid, target: target)
        {
            Hidden = true,
            Broadcast = false,
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DistanceThreshold = 1
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        Random random = new Random();
        int index = random.Next(component.WordsArray.Length);
        string message = component.WordsArray[index];

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, true);

        args.Handled = true;
    }

    private void OnRecruitmentDoAfter(EntityUid uid, UnitologyHeadComponent component, UnitologistRecruitmentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target;

        if (!_mindSystem.TryGetMind(target.Value, out _, out _))
            return;

        _unitologyRule.TryGrantUnitologyRole(target.Value, UnitologyRuleSystem.RegularUnitologyAntagRole);
    }

    private void OnOrder(EntityUid uid, UnitologyHeadComponent component, OrderToSlaveActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;

        if (!HasComp<UnitologyEnslavedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель должна быть подчинена!"), uid, uid);
            return;
        }

        if (!HasComp<StunSlaveComponent>(target))
        {
            AddComp<StunSlaveComponent>(target);
            _popup.PopupEntity(Loc.GetString("Цель парализованна."), uid, uid);
        }
        else
        {
            RemComp<StunSlaveComponent>(target);
            _popup.PopupEntity(Loc.GetString("Цель может двигаться."), uid, uid);
        }

        args.Handled = true;

    }
    private void OnHeadUnitology(EntityUid uid, UnitologyHeadComponent component, UnitologyHeadActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;
        if (!IsCanTransfer(uid, target))
            return;

        args.Handled = true;

        TransferHeadImplants(uid, target);

        RemComp<UnitologyHeadComponent>(uid);

        AddComp<UnitologyHeadComponent>(target);
    }

    private void TransferHeadImplants(EntityUid oldHead, EntityUid newHead)
    {
        if (!TryComp<ImplantedComponent>(oldHead, out var oldImplanted))
            return;

        var newImplanted = EnsureComp<ImplantedComponent>(newHead);
        var existing = newImplanted.ImplantContainer.ContainedEntities
            .Select(implant => Prototype(implant))
            .Where(proto => proto != null)
            .Select(proto => proto!.ID)
            .ToHashSet();

        foreach (var implant in oldImplanted.ImplantContainer.ContainedEntities.ToArray())
        {
            var prototype = Prototype(implant);
            if (prototype == null || !HeadImplants.Contains(prototype.ID))
                continue;

            if (existing.Contains(prototype.ID))
            {
                _implants.ForceRemove((oldHead, oldImplanted), implant);
                continue;
            }

            if (!_containers.Remove(implant, oldImplanted.ImplantContainer, force: true))
                continue;

            _containers.Insert(implant, newImplanted.ImplantContainer);
            existing.Add(prototype.ID);
        }
    }

    private bool IsCanTransfer(EntityUid uid, EntityUid target)
    {
        if (!HasComp<UnitologyComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель должна быть юнитологом!"), uid, uid);
            return false;
        }

        if (HasComp<UnitologyHeadComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель уже обладает вашими знаниями и положением!"), uid, uid);
            return false;
        }

        if (HasComp<UnitologyEnslavedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель не может быть порабощенным!"), uid, uid);
            return false;
        }

        if (HasComp<NecromorfComponent>(target) || HasComp<ZombieComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель не может быть выбрана!"), uid, uid);
            return false;
        }

        return true;
    }
}
