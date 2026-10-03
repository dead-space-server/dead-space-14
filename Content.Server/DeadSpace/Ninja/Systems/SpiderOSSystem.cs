// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Interaction.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.RetractableItemAction;
using Content.Shared.Ninja.Components;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class SpiderOSSystem : SharedSpiderOSSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedNinjaAppearanceSystem _appearance = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly RetractableItemActionSystem _retractableItemAction = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _lockedBySpiderOS = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SpiderOSComponent>(SpiderOSUiKey.Key, subs =>
        {
            subs.Event<SpiderOSSelectModuleMessage>(OnSelectModule);
            subs.Event<SpiderOSSetAppearanceMessage>(OnSetAppearance);
            subs.Event<SpiderOSSetSuitPowerMessage>(OnSetSuitPower);
            subs.Event<SpiderOSSecureRequestMessage>(OnSecureRequest);
            subs.Event<SpiderOSShuttleControlMessage>(OnShuttleControl);
        });

        SubscribeLocalEvent<SpiderOSComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<SpiderOSComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpiderOSComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<SpiderOSComponent> ent, ref ComponentShutdown args)
    {
        UnlockTrackedItems(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpiderOSComponent>();
        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.SuitActivated)
                continue;

            var wearer = SharedTransform.GetParentUid(suitUid);
            if (!wearer.IsValid())
                continue;

            if (!_itemSlots.TryGetSlot(suitUid, "cell_slot", out var slot) || slot.Item is not { } batteryUid)
                continue;

            _battery.TryUseCharge(batteryUid, comp.EnergyConsumption * frameTime);
        }
    }

    private void OnMapInit(Entity<SpiderOSComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.Actions)
        {
            AddActionToContainer(ent.Owner, action);
        }
    }

    private void OnBuiOpened(Entity<SpiderOSComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not SpiderOSUiKey.Key)
        {
            return;
        }

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnSelectModule(Entity<SpiderOSComponent> suit, ref SpiderOSSelectModuleMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor) ||
            comp.SuitActivated ||
            !TryGetSkill(comp, args.Category, args.Tier, out _))
        {
            return;
        }

        if (comp.LockedTiers.Contains(args.Tier) || comp.SelectedModules.ContainsKey(args.Tier))
        {
            return;
        }

        comp.SelectedModules[args.Tier] = args.Category;
        comp.LockedTiers.Add(args.Tier);

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void OnShuttleControl(Entity<SpiderOSComponent> suit, ref SpiderOSShuttleControlMessage args)
    {
        var suitUid = suit.Owner;

        if (!IsAuthorized(suitUid, args.Actor))
            return;

        var wearer = SharedTransform.GetParentUid(suitUid);
        if (!wearer.IsValid())
        {
            _popup.PopupEntity(Loc.GetString("spider-os-shuttle-control-fail-not-worn"), suitUid, args.Actor);
            return;
        }

        if (_ui.IsUiOpen(suitUid, ShuttleConsoleUiKey.Key))
        {
            _ui.CloseUi(suitUid, ShuttleConsoleUiKey.Key, args.Actor);
            return;
        }

        if (!_shuttleConsole.TryRefreshDroneTarget(suitUid))
        {
            _popup.PopupEntity(Loc.GetString("spider-os-shuttle-control-fail"), suitUid, args.Actor);
            return;
        }

        _ui.OpenUi(suitUid, ShuttleConsoleUiKey.Key, args.Actor);

        _shuttleConsole.RefreshShuttleConsole(suitUid);

        if (TryComp<ShuttleConsoleComponent>(suitUid, out var shuttleComp))
        {
            EnsureComp<PilotComponent>(args.Actor);
            _shuttleConsole.AddPilot(suitUid, args.Actor, shuttleComp);
        }
    }

    private void OnSetAppearance(Entity<SpiderOSComponent> suit, ref SpiderOSSetAppearanceMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor) ||
            comp.SuitActivated ||
            !Enum.IsDefined(args.Colorway) ||
            !Enum.IsDefined(args.Style))
        {
            return;
        }

        var helmet = args.Style == NinjaStyle.New || args.Helmet;

        if (comp.PendingColorway == args.Colorway && comp.PendingHelmet == helmet &&
            comp.PendingStyle == args.Style)
        {
            return;
        }

        comp.PendingColorway = args.Colorway;
        comp.PendingHelmet = helmet;
        comp.PendingStyle = args.Style;

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void OnSetSuitPower(Entity<SpiderOSComponent> suit, ref SpiderOSSetSuitPowerMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor))
        {
            return;
        }

        if (args.Activated)
        {
            if (comp.SuitActivated)
            {
                return;
            }

            if (!Proto.TryIndex(comp.ActivationBootScript, out SpiderOSBootPrototype? boot) ||
                !RunBootScriptChecks(suitUid, args.Actor, boot))
            {
                return;
            }

            ActivateSuit(suitUid, comp);
        }
        else
        {
            if (!comp.SuitActivated)
            {
                return;
            }

            DeactivateSuit(suitUid, comp);
        }

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void ActivateSuit(EntityUid suitUid, SpiderOSComponent comp)
    {
        comp.SuitActivated = true;
        ApplyPendingAppearance(suitUid, comp);

        foreach (var (tier, category) in comp.SelectedModules)
        {
            if (comp.ActivatedTiers.Contains(tier) || !TryGetSkill(comp, category, tier, out var skill))
            {
                continue;
            }

            GrantSkillComponents(suitUid, skill);
            GrantSkillActions(suitUid, skill);
            comp.ActivatedTiers.Add(tier);
        }

        var wearer = SharedTransform.GetParentUid(suitUid);
        if (!wearer.IsValid())
            return;

        SetAllLocked(wearer, suitUid, true);
        _actions.GrantContainedActions(wearer, suitUid);

        var powerChanged = new SpiderOSPowerChangedEvent(suitUid, wearer, true);
        RaiseLocalEvent(suitUid, ref powerChanged);
    }

    private void DeactivateSuit(EntityUid suitUid, SpiderOSComponent comp)
    {
        comp.SuitActivated = false;

        UnlockTrackedItems(suitUid);

        var wearer = SharedTransform.GetParentUid(suitUid);
        if (!wearer.IsValid())
            return;

        SetAllLocked(wearer, suitUid, false);
        RemoveGrantedActions(suitUid, wearer, comp);

        var powerChanged = new SpiderOSPowerChangedEvent(suitUid, wearer, false);
        RaiseLocalEvent(suitUid, ref powerChanged);
    }

    private void UnlockTrackedItems(EntityUid suitUid)
    {
        if (!_lockedBySpiderOS.Remove(suitUid, out var locked))
            return;

        foreach (var uid in locked)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            RemComp<UnremoveableComponent>(uid);
        }
    }

    private void ApplyPendingAppearance(EntityUid suitUid, SpiderOSComponent comp)
    {
        if (TryComp<NinjaAppearanceComponent>(suitUid, out var appearance))
        {
            _appearance.SetAppearance((suitUid, appearance), comp.PendingColorway, comp.PendingHelmet, comp.PendingStyle);
        }
    }

    public void RestoreState(EntityUid suitUid, SpiderOSComponent source)
    {
        var comp = EnsureComp<SpiderOSComponent>(suitUid);
        comp.LockedTiers = new HashSet<int>(source.LockedTiers);
        comp.SelectedModules = new Dictionary<int, NinjaSkillsCategory>(source.SelectedModules);
        comp.ActivatedTiers = new HashSet<int>();
        comp.Skills = source.Skills;

        foreach (var (tier, category) in comp.SelectedModules.ToArray())
        {
            if (TryGetSkill(comp, category, tier, out var skill) && !skill.TransferOnSecondChance)
            {
                comp.SelectedModules.Remove(tier);
                comp.LockedTiers.Remove(tier);
            }
        }

        comp.Actions = new List<EntProtoId>(source.Actions);
        comp.PendingColorway = source.PendingColorway;
        comp.PendingHelmet = source.PendingHelmet;
        comp.PendingStyle = source.PendingStyle;
        comp.SuitActivated = false;
        Dirty(suitUid, comp);

        ApplyPendingAppearance(suitUid, comp);

        foreach (var action in comp.Actions)
        {
            AddActionToContainer(suitUid, action);
        }
    }

    private void GrantSkillComponents(EntityUid suitUid, NinjaSkill skill)
    {
        var toAdd = new ComponentRegistry();
        foreach (var (name, entry) in skill.Components)
        {
            if (!HasComp(suitUid, entry.Component.GetType()))
            {
                toAdd[name] = entry;
            }
        }

        if (toAdd.Count > 0)
        {
            EntityManager.AddComponents(suitUid, toAdd);
        }
    }

    private void GrantSkillActions(EntityUid suitUid, NinjaSkill skill)
    {
        if (skill.Actions == null || skill.Actions.Count == 0)
        {
            return;
        }

        foreach (var actionProto in skill.Actions)
        {
            AddActionToContainer(suitUid, actionProto);
        }
    }

    private void AddActionToContainer(EntityUid suitUid, EntProtoId actionProto)
    {
        var container = EnsureComp<ActionsContainerComponent>(suitUid);

        foreach (var existing in container.Container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype?.ID == actionProto.Id)
            {
                return;
            }
        }

        _actionContainer.AddAction(suitUid, actionProto, container);
    }

    private void RemoveGrantedActions(EntityUid suitUid, EntityUid wearer, SpiderOSComponent comp)
    {
        var grantedProtos = new HashSet<string>();

        if (TryComp<NinjaSuitComponent>(suitUid, out var suitComp))
        {
            grantedProtos.Add(suitComp.RecallKatanaAction);
        }

        foreach (var action in comp.Actions)
        {
            grantedProtos.Add(action.Id);
        }

        foreach (var (tier, category) in comp.SelectedModules)
        {
            if (!TryGetSkill(comp, category, tier, out var skill) || skill.Actions == null)
            {
                continue;
            }

            foreach (var action in skill.Actions)
            {
                grantedProtos.Add(action.Id);
            }
        }

        if (!TryComp<ActionsContainerComponent>(suitUid, out var container))
        {
            return;
        }

        foreach (var contained in container.Container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype?.ID is { } protoId && grantedProtos.Contains(protoId))
            {
                if (TryComp<RetractableItemActionComponent>(contained, out var retractableActionComponent) && retractableActionComponent.ActionItemUid != null)
                {
                    _retractableItemAction.RetractRetractableItem(wearer, retractableActionComponent.ActionItemUid.Value, contained);
                }
                _actions.RemoveProvidedAction(wearer, suitUid, contained);
            }
        }
    }

    private void OnSecureRequest(Entity<SpiderOSComponent> suit, ref SpiderOSSecureRequestMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor))
        {
            _ui.ServerSendUiMessage(suitUid, SpiderOSUiKey.Key,
                new SpiderOSSecureConfirmedMessage(false, "spider-os-boot-fail-auth"), args.Actor);
            return;
        }

        var wearer = SharedTransform.GetParentUid(suitUid);
        if (!wearer.IsValid())
        {
            _ui.ServerSendUiMessage(suitUid, SpiderOSUiKey.Key,
                new SpiderOSSecureConfirmedMessage(false, "spider-os-boot-fail-not-worn"), args.Actor);
            return;
        }

        if (args.Secure)
        {
            if (!RunBootCheck(suitUid, args.Actor, args.Check, out var reason))
            {
                _ui.ServerSendUiMessage(suitUid, SpiderOSUiKey.Key,
                    new SpiderOSSecureConfirmedMessage(false, reason));
                return;
            }

            SetLocked(wearer, suitUid, args.Check, true);
            _ui.ServerSendUiMessage(suitUid, SpiderOSUiKey.Key,
                new SpiderOSSecureConfirmedMessage(true));
        }
        else
        {
            if (!comp.SuitActivated)
            {
                SetAllLocked(wearer, suitUid, false);
            }

            _ui.ServerSendUiMessage(suitUid, SpiderOSUiKey.Key,
                new SpiderOSSecureConfirmedMessage(true));
        }
    }

    private void SetAllLocked(EntityUid wearer, EntityUid suitUid, bool locked)
    {
        ApplyLock(suitUid, suitUid, locked);

        foreach (var slot in SuitHardwareSlots.Values)
        {
            if (Inventory.TryGetSlotEntity(wearer, slot, out var item))
            {
                ApplyLock(suitUid, item.Value, locked);
            }
        }
    }

    private void SetLocked(EntityUid wearer, EntityUid suitUid, SpiderOSBootCheck check, bool locked)
    {
        if (check == SpiderOSBootCheck.SuitFasten)
        {
            ApplyLock(suitUid, suitUid, locked);
            return;
        }

        if (!SuitHardwareSlots.TryGetValue(check, out var slot))
            return;

        if (Inventory.TryGetSlotEntity(wearer, slot, out var item))
        {
            ApplyLock(suitUid, item.Value, locked);
        }
    }

    private void ApplyLock(EntityUid suitUid, EntityUid uid, bool locked)
    {
        if (locked)
        {
            if (HasComp<UnremoveableComponent>(uid))
                return;

            AddComp(uid, new UnremoveableComponent { DeleteOnDrop = false }, true);

            if (!_lockedBySpiderOS.TryGetValue(suitUid, out var suitLocked))
            {
                suitLocked = new HashSet<EntityUid>();
                _lockedBySpiderOS[suitUid] = suitLocked;
            }

            suitLocked.Add(uid);
            return;
        }

        if (!_lockedBySpiderOS.TryGetValue(suitUid, out var lockedSet) || !lockedSet.Remove(uid))
            return;

        RemComp<UnremoveableComponent>(uid);

        if (lockedSet.Count == 0)
            _lockedBySpiderOS.Remove(suitUid);
    }

    private void UpdateUi(EntityUid suitUid, SpiderOSComponent comp)
    {
        var state = new SpiderOSBoundUserInterfaceState(
            comp.LockedTiers,
            comp.SelectedModules,
            comp.ActivatedTiers,
            comp.Skills.Id,
            comp.PendingColorway,
            comp.PendingHelmet,
            comp.PendingStyle,
            comp.SuitActivated);
        _ui.SetUiState(suitUid, SpiderOSUiKey.Key, state);
    }
}