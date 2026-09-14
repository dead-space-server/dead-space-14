using Content.Shared._CM14.Input;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
namespace Content.Shared._CM14.Weapons.Common;
public sealed class UniqueActionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<UniqueActionComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMUniqueAction,
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is { } userUid)
                            TryUniqueAction(userUid);
                    },
                    handle: false))
            .Register<UniqueActionSystem>();
    }
    public override void Shutdown()
    {
        CommandBinds.Unregister<UniqueActionSystem>();
    }
    private void OnGetVerbs(Entity<UniqueActionComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (!_actionBlockerSystem.CanInteract(args.User, args.Target))
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => TryUniqueAction(user, ent.Owner),
            Text = "Unique action",
        });
    }
    private void TryUniqueAction(EntityUid userUid)
    {
        var activeItem = _hands.GetActiveItem(userUid);
        if (activeItem == null ||
            !_entityManager.TryGetComponent(activeItem.Value, out UniqueActionComponent? uniqueActionComponent))
            return;
        if (!uniqueActionComponent.Running)
            return;
        TryUniqueAction(userUid, activeItem.Value);
    }
    private void TryUniqueAction(EntityUid userUid, EntityUid targetUid)
    {
        if (!_actionBlockerSystem.CanInteract(userUid, targetUid))
            return;
        RaiseLocalEvent(targetUid, new UniqueActionEvent(userUid));
    }
}
