using Content.Shared._CM14.Input;
using Content.Shared._CM14.Weapons.Common;
using Content.Shared.ActionBlocker;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input.Binding;

namespace Content.Shared._CM14.Weapons.Ranged;

public abstract class SharedPumpActionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PumpActionComponent, ExaminedEvent>(OnExamined, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<PumpActionComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PumpActionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<PumpActionComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PumpActionComponent, UniqueActionEvent>(OnUniqueAction);
        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMUniqueAction,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (session?.AttachedEntity is { } entity)
                        TryPump(entity);
                }, handle: false))
            .Register<SharedPumpActionSystem>();
    }
    public override void Shutdown()
    {
        CommandBinds.Unregister<SharedPumpActionSystem>();
    }

    protected virtual void OnExamined(Entity<PumpActionComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cm-gun-pump-examine"), 1);
    }

    private void OnGetVerbs(Entity<PumpActionComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        if (!_actionBlocker.CanInteract(user, args.Target))
            return;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => TryPump(user, ent),
            Text = "Pump"
        });
    }
    protected virtual void OnAttemptShoot(Entity<PumpActionComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.Running || ent.Comp.Pumped)
            args.Cancelled = true;
    }

    private void OnGunShot(Entity<PumpActionComponent> ent, ref GunShotEvent args)
    {
        var activeItem = _hands.GetActiveItem(args.User);
        if (activeItem != null &&
            TryComp(activeItem.Value, out PumpActionComponent? pump))
        {
            var ammo = new GetAmmoCountEvent();
            RaiseLocalEvent(activeItem.Value, ref ammo);
            if (ammo.Count <= 0)
            {
                _popup.PopupClient(Loc.GetString("cm-gun-no-ammo-message"), args.User, args.User);
                return;
            }
        }

        TryPump(args.User, (ent.Owner, ent.Comp));
    }

    private void OnUniqueAction(Entity<PumpActionComponent> ent, ref UniqueActionEvent args)
    {
        var activeItem = _hands.GetActiveItem(args.UserUid);
        if (activeItem != null &&
            TryComp(activeItem.Value, out PumpActionComponent? pump))
        {
            var ammo = new GetAmmoCountEvent();
            RaiseLocalEvent(activeItem.Value, ref ammo);
            if (ammo.Count <= 0)
            {
                _popup.PopupClient(Loc.GetString("cm-gun-no-ammo-message"), args.UserUid, args.UserUid);
                return;
            }
            TryPump(args.UserUid, (activeItem.Value, pump));
        }
    }

    private void TryPump(EntityUid user)
    {
        var activeItem = _hands.GetActiveItem(user);
        if (activeItem != null &&
            TryComp(activeItem.Value, out PumpActionComponent? pump))
        {
            TryPump(user, (activeItem.Value, pump));
        }
    }

    private void TryPump(EntityUid user, Entity<PumpActionComponent> ent)
    {
        if (!ent.Comp.Running || ent.Comp.Pumped)
            return;

        ent.Comp.Pumped = true;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.Sound, ent, user);
    }
}
