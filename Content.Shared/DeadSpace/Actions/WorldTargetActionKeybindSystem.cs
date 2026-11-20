using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.DeadSpace.Actions;

public sealed partial class WorldTargetActionKeybindSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.UseWorldTargetAction, new PointerInputCmdHandler(HandleUseWorldTargetAction))
            .Register<WorldTargetActionKeybindSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<WorldTargetActionKeybindSystem>();
        base.Shutdown();
    }

    private bool HandleUseWorldTargetAction(ICommonSession? session, EntityCoordinates coordinates, EntityUid target)
    {
        if (_net.IsClient)
            return false;

        if (session?.AttachedEntity is not { Valid: true } user || !Exists(user))
            return false;

        if (!TryComp<WorldTargetActionKeybindComponent>(user, out var component))
            return false;

        TryUseWorldTargetAction((user, component), coordinates, target);
        return false;
    }

    /// <summary>
    ///     Attempts to execute the world-target action bound to <paramref name="ent"/> at the given coordinates.
    ///     Resolves <see cref="WorldTargetActionKeybindComponent"/> on the entity, locates the matching
    ///     <see cref="WorldTargetActionComponent"/> action via <c>TryGetAction</c>, then sends it through the
    ///     standard validated action execution path with <paramref name="coordinates"/> and an optional
    ///     <paramref name="target"/>.
    /// </summary>
    /// <param name="ent">The entity that owns the keybind component. Component may be <c>null</c>; it will be resolved internally.</param>
    /// <param name="coordinates">World coordinates at which the action should be aimed.</param>
    /// <param name="target">
    ///     Optional entity under the cursor. Only used when the action also has an
    ///     <see cref="EntityTargetActionComponent"/> and the entity is valid and exists.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the action was located and passed server-side validation;
    ///     <c>false</c> if the component could not be resolved, no matching action was found, or validation failed.
    /// </returns>
    public bool TryUseWorldTargetAction(
        Entity<WorldTargetActionKeybindComponent?> ent,
        EntityCoordinates coordinates,
        EntityUid target)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!TryGetAction(ent, out var actionEnt))
            return false;

        NetEntity? entityTarget = HasComp<EntityTargetActionComponent>(actionEnt) && target.IsValid() && Exists(target)
            ? GetNetEntity(target)
            : null;

        var request = new RequestPerformActionEvent(
            GetNetEntity(actionEnt),
            entityTarget,
            GetNetCoordinates(coordinates));

        return _actions.TryPerformAction(request, ent.Owner);
    }

    private bool TryGetAction(
        Entity<WorldTargetActionKeybindComponent?> ent,
        out EntityUid actionEnt)
    {
        actionEnt = default;

        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!TryComp<ActionsComponent>(ent.Owner, out var actions))
            return false;

        foreach (var candidate in actions.Actions)
        {
            var prototype = Prototype(candidate);
            if (prototype is null || prototype.ID != ent.Comp.Action.Id)
                continue;

            if (!HasComp<WorldTargetActionComponent>(candidate))
                return false;

            actionEnt = candidate;
            return true;
        }

        return false;
    }
}
