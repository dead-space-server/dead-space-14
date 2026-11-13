using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.LawConfigurator.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Content.Shared.DoAfter;
using Content.Shared.Xenoborgs.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.LawConfigurator.Systems;

public sealed partial class LawConfiguratorSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LawConfiguratorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LawConfiguratorComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<LawConfiguratorComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<LawConfiguratorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<LawConfiguratorComponent, LawConfiguratorDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnDoAfterComplete(EntityUid uid, LawConfiguratorComponent comp, LawConfiguratorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        var user = args.Args.User;

        if (HasComp<XenoborgComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("law-configurator-xenoborg-blocked"), user, user);
            args.Handled = true;
            return;
        }

        // Получаем плату из слота конфигуратора
        if (!_itemSlots.TryGetSlot(uid, "circuit_holder", out var slot) || slot.Item == null)
        {
            _popup.PopupClient(Loc.GetString("law-configurator-requires-board"), user, user);
            return;
        }

        var board = slot.Item.Value;

        // Панель всё ещё открыта?
        if (comp.RequireOpenPanel && TryComp<WiresPanelComponent>(target, out var panel) && !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("law-configurator-requires-open-panel"), user, user);
            return;
        }

        // Основная логика замены законов
        var ev = new ConfigureLawsFromBoardEvent(target, user, board);
        RaiseLocalEvent(ev);

        _popup.PopupClient($"Законы {Identity.Name(target, EntityManager)} успешно заменены.", user, user);

        // Админ логи
        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):player} заменил законы {ToPrettyString(target):target} платой {ToPrettyString(board):board}");

        // Воспроизводим звук
        if (comp.SuccessSound != null)
            _audio.PlayPredicted(comp.SuccessSound, uid, user);

        args.Handled = true;
    }

    private void OnComponentInit(EntityUid uid, LawConfiguratorComponent component, ComponentInit args)
    {
        UpdateBoardState(uid);
    }

    private void OnItemSlotChanged(EntityUid uid, LawConfiguratorComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != "circuit_holder")
            return;

        UpdateBoardState(uid);
    }

    private void UpdateBoardState(EntityUid uid)
    {
        if (!TryComp<LawConfiguratorComponent>(uid, out var component))
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
        {
            if (component.HasBoard)
            {
                component.HasBoard = false;
                Dirty(uid, component);
            }
            return;
        }

        var hasBoard = _itemSlots.TryGetSlot(uid, "circuit_holder", out var slot, slots) && slot.Item != null;

        if (component.HasBoard != hasBoard)
        {
            component.HasBoard = hasBoard;
            Dirty(uid, component);
        }
    }

    private void OnAfterInteract(EntityUid uid, LawConfiguratorComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<SiliconLawBoundComponent>(target, out var siliconLaw))
            return;

        if (HasComp<XenoborgComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("law-configurator-xenoborg-blocked"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!_itemSlots.TryGetSlot(uid, "circuit_holder", out var slot) || slot.Item == null)
        {
            _popup.PopupClient(
                Loc.GetString("law-configurator-requires-board"),
                args.User,
                args.User);
            return;
        }

        // Требуется открытая панель юнита?
        if (comp.RequireOpenPanel && TryComp<WiresPanelComponent>(target, out var panel) && !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("law-configurator-requires-open-panel"), args.User, args.User);
            return;
        }

        var board = slot.Item.Value;
        var targetName = Identity.Name(target, EntityManager);

        // Запускаем прогресс-бар с проверками
        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(10.0),
                new LawConfiguratorDoAfterEvent(),
                uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            // Проверяем, что плата всё ещё в контейнере и панель всё ещё открыта
            ExtraCheck = () =>
            {
                // Проверяем, что плата всё ещё в слоте
                var boardCheck = _itemSlots.TryGetSlot(uid, "circuit_holder", out var currentSlot)
                    && currentSlot.Item == board;

                if (!boardCheck)
                    return false;

                // Проверяем, что панель открыта
                if (comp.RequireOpenPanel && TryComp<WiresPanelComponent>(target, out var currentPanel))
                    return currentPanel.Open;

                return true;
            }
        };

        if (!_doAfterSystem.TryStartDoAfter(doAfterEventArgs))
        {
            _popup.PopupClient("Не удалось начать конфигурацию законов.", args.User, args.User);
            return;
        }

        _popup.PopupClient($"Начинаю конфигурацию законов {targetName}...", args.User, args.User);
        args.Handled = true;
    }
}

// Событие DoAfter для конфигуратора законов
[Serializable, NetSerializable]
public sealed partial class LawConfiguratorDoAfterEvent : SimpleDoAfterEvent
{
    // Без хранения каких-либо данных о плате
}

// Событие для запроса замены законов на законы с платы
public sealed partial class ConfigureLawsFromBoardEvent : EntityEventArgs
{
    public EntityUid Target { get; }
    public EntityUid User { get; }
    public EntityUid Board { get; }

    public ConfigureLawsFromBoardEvent(EntityUid target, EntityUid user, EntityUid board)
    {
        Target = target;
        User = user;
        Board = board;
    }
}
