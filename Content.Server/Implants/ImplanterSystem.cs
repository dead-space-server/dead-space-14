using System.Linq;
using Content.Server.DeadSpace.Administration;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server.Implants;

public sealed partial class ImplanterSystem : SharedImplanterSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeImplanted();

        SubscribeLocalEvent<ImplanterComponent, AfterInteractEvent>(OnImplanterAfterInteract);

        SubscribeLocalEvent<ImplanterComponent, ImplantEvent>(OnImplant);
        SubscribeLocalEvent<ImplanterComponent, DrawEvent>(OnDraw);
    }

    // TODO: This all needs to be moved to shared and predicted.
    private void OnImplanterAfterInteract(EntityUid uid, ImplanterComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || args.Handled)
            return;

        var target = args.Target.Value;
        if (!CheckTarget(target, component.Whitelist, component.Blacklist))
            return;

        //TODO: Rework when surgery is in for implant cases
        if (component.CurrentMode == ImplanterToggleMode.Draw && !component.ImplantOnly)
        {
            TryDraw(component, args.User, target, uid);
        }
        else
        {
            if (!CanImplantLimb(target, component, out _) //DS-14
                && !CanImplant(args.User, target, uid, component, out var implant, out _))
            {
                // no popup if implant doesn't exist
                if (implant == null)
                    return;

                // show popup to the user saying implant failed
                var name = Identity.Name(target, EntityManager, args.User);
                var msg = Loc.GetString("implanter-component-implant-failed", ("implant", implant), ("target", name));
                _popup.PopupEntity(msg, target, args.User);
                // prevent further interaction since popup was shown
                args.Handled = true;
                return;
            }

            // DS14-start
            // Most implanters remain instant on self, while special implanters can opt into the regular do-after.
            if (args.User == target && component.InstantSelfImplant)
            {
                PropagateAntagPurchase(uid, component);
            // DS14-end
                Implant(target, target, uid, component);
            }
            else
                TryImplant(component, args.User, target, uid);
        }

        args.Handled = true;
    }

    /// <summary>
    /// Attempt to implant someone else.
    /// </summary>
    /// <param name="component">Implanter component</param>
    /// <param name="user">The entity using the implanter</param>
    /// <param name="target">The entity being implanted</param>
    /// <param name="implanter">The implanter being used</param>
    public void TryImplant(ImplanterComponent component, EntityUid user, EntityUid target, EntityUid implanter)
    {
        var args = new DoAfterArgs(EntityManager, user, component.ImplantTime, new ImplantEvent(), implanter, target: target, used: implanter)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            AttemptFrequency = component.ImplantAttemptFrequency, // DS14
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        // DS14-start
        if (user == target)
        {
            _popup.PopupEntity(
                Loc.GetString("implanter-component-implanting-self"),
                target,
                user,
                PopupType.LargeCaution);
            return;
        }

        var targetName = Identity.Entity(target, EntityManager);
        _popup.PopupEntity(
            Loc.GetString("implanter-component-implanting-user", ("target", targetName)),
            target,
            user);

        var userName = Identity.Entity(user, EntityManager);
        _popup.PopupEntity(
            Loc.GetString("implanter-component-implanting-target", ("user", userName)),
            user,
            target,
            PopupType.LargeCaution);
        // DS14-end
    }

    /// <summary>
    /// Try to remove an implant and store it in an implanter
    /// </summary>
    /// <param name="component">Implanter component</param>
    /// <param name="user">The entity using the implanter</param>
    /// <param name="target">The entity getting their implant removed</param>
    /// <param name="implanter">The implanter being used</param>
    //TODO: Remove when surgery is in
    public void TryDraw(ImplanterComponent component, EntityUid user, EntityUid target, EntityUid implanter)
    {
        var args = new DoAfterArgs(EntityManager, user, component.DrawTime, new DrawEvent(), implanter, target: target, used: implanter)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("injector-component-needle-injecting-user"), target, user);

    }

    private void OnImplant(EntityUid uid, ImplanterComponent component, ImplantEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null || args.Used == null)
            return;

        PropagateAntagPurchase(args.Used.Value, component); // DS14

        Implant(args.User, args.Target.Value, args.Used.Value, component);

        args.Handled = true;
    }

    // DS14-start
    private void PropagateAntagPurchase(EntityUid implanter, ImplanterComponent component)
    {
        var contained = component.ImplanterSlot.ContainerSlot?.ContainedEntities;
        if (contained == null || contained.Count == 0 ||
            !TryComp<AntagPurchasedEntityComponent>(implanter, out var purchase))
        {
            return;
        }

        var implantEntity = contained[0];
        EnsureComp<AntagPurchasedEntityComponent>(implantEntity).MindId = purchase.MindId;
    }
    // DS14-end

    private void OnDraw(EntityUid uid, ImplanterComponent component, DrawEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used == null || args.Target == null)
            return;

        Draw(args.Used.Value, args.User, args.Target.Value, component);

        args.Handled = true;
    }
}
