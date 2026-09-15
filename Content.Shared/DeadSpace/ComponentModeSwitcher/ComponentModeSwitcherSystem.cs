// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.DeadSpace.ComponentModeSwitcher;

/// <summary>
/// Generic mode switcher driven by an arbitrary list of component registries.
/// The mode of the item in the active hand is cycled with its dedicated keybind.
/// </summary>
public sealed class ComponentModeSwitcherSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RCDSystem _rcd = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ComponentModeSwitcherComponent, MapInitEvent>(OnMapInit);

        CommandBinds.Builder
            .Bind(DeadSpaceKeys.SwitchComponentMode, new PointerInputCmdHandler(HandleSwitchMode))
            .Register<ComponentModeSwitcherSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<ComponentModeSwitcherSystem>();
        base.Shutdown();
    }

    private bool HandleSwitchMode(ICommonSession? session, EntityCoordinates coordinates, EntityUid target)
    {
        if (_net.IsClient || session?.AttachedEntity is not { Valid: true } user)
            return false;

        if (_hands.GetActiveItem(user) is not { } item ||
            !TryComp<ComponentModeSwitcherComponent>(item, out var switcher))
            return false;

        TryCycleMode((item, switcher), user);
        return false;
    }

    private void OnMapInit(Entity<ComponentModeSwitcherComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.Modes.Count == 0)
            return;

        ent.Comp.CurrentMode = Math.Clamp(ent.Comp.CurrentMode, 0, ent.Comp.Modes.Count - 1);

        foreach (var mode in ent.Comp.Modes)
            EntityManager.RemoveComponents(ent.Owner, mode.Components);

        EntityManager.AddComponents(ent.Owner, ent.Comp.Modes[ent.Comp.CurrentMode].Components, true);
        InitializeAddedComponents(ent.Owner);
        Dirty(ent);
    }

    public bool TryCycleMode(Entity<ComponentModeSwitcherComponent?> ent, EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Modes.Count < 2)
            return false;

        var previous = ent.Comp.CurrentMode;
        var next = (previous + 1) % ent.Comp.Modes.Count;

        var previousComponents = ent.Comp.Modes[previous].Components;
        if (ent.Comp.PreserveState)
            ent.Comp.SavedStates[previous] = CaptureState(ent.Owner, previousComponents);

        EntityManager.RemoveComponents(ent.Owner, previousComponents);

        var nextComponents = ent.Comp.Modes[next].Components;
        EntityManager.AddComponents(ent.Owner, nextComponents, true);

        if (ent.Comp.PreserveState && ent.Comp.SavedStates.TryGetValue(next, out var saved))
            RestoreState(ent.Owner, saved);
        else
            InitializeAddedComponents(ent.Owner);

        ent.Comp.CurrentMode = next;
        Dirty(ent);

        var mode = ent.Comp.Modes[next];
        if (user != null)
            _popup.PopupEntity(Loc.GetString(mode.Popup ?? mode.Name), ent.Owner, user.Value);

        var ev = new ComponentModeChangedEvent(previous, next, mode.Name, user);
        RaiseLocalEvent(ent.Owner, ref ev);
        return true;
    }

    private void InitializeAddedComponents(EntityUid uid)
    {
        if (TryComp<RCDComponent>(uid, out var rcd))
            _rcd.InitializeDevice((uid, rcd));
    }

    private void RestoreState(EntityUid uid, ComponentRegistry state)
    {
        foreach (var (name, entry) in state)
        {
            var registration = Factory.GetRegistration(name);
            if (!EntityManager.TryGetComponent(uid, registration.Type, out var component))
                continue;

            _serialization.CopyTo(entry.Component, ref component, notNullableOverride: true);
            Dirty(uid, component);
        }
    }

    private ComponentRegistry CaptureState(EntityUid uid, ComponentRegistry components)
    {
        var state = new ComponentRegistry();

        foreach (var (name, entry) in components)
        {
            var registration = Factory.GetRegistration(name);
            if (!EntityManager.TryGetComponent(uid, registration.Type, out var component))
                continue;

            var copy = (IComponent) _serialization.CreateCopy(component, notNullableOverride: true);
            state.Add(name, new EntityPrototype.ComponentRegistryEntry(copy, entry.Mapping));
        }

        return state;
    }
}