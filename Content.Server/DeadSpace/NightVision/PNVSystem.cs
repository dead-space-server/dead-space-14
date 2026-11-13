using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.NightVision;

public sealed partial class PNVSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const SlotFlags ValidSlots =
            SlotFlags.HEAD |
            SlotFlags.EYES |
            SlotFlags.MASK
        ;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PNVComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<PNVComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid entity, PNVComponent comp, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ValidSlots) == 0)
            return;

        if (TryComp<NightVisionComponent>(args.Equipee, out var existingNightVision))
        {
            if (comp.PreviousNightVision != null)
                return;

            comp.PreviousNightVision = new PreviousNightVisionState
            {
                Color = existingNightVision.Color,
                ActivateSound = existingNightVision.ActivateSound,
                Duration = existingNightVision.Duration,
                Animation = existingNightVision.Animation,
                Desaturation = existingNightVision.Desaturation,
                ActionToggleNightVision = existingNightVision.ActionToggleNightVision,
                ActionToggleNightVisionEntity = existingNightVision.ActionToggleNightVisionEntity,
            };

            ApplyPnvNightVision(args.Equipee, comp, existingNightVision, comp.PreviousNightVision);
            return;
        }

        var nightVisionComp = new NightVisionComponent(comp.Color, comp.ActivateSound, comp.Animation, comp.Desaturation);
        nightVisionComp.ActionToggleNightVision = comp.ActionToggleNightVision;
        comp.HasNightVision = true;

        AddComp(args.Equipee, nightVisionComp);
    }

    private void OnGotUnequipped(EntityUid entity, PNVComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.HasNightVision && HasComp<NightVisionComponent>(args.Equipee))
        {
            RemComp<NightVisionComponent>(args.Equipee);
            comp.HasNightVision = false;
            return;
        }

        if (comp.PreviousNightVision == null ||
            !TryComp<NightVisionComponent>(args.Equipee, out var nightVision))
        {
            comp.PreviousNightVision = null;
            return;
        }

        RestorePreviousNightVision(args.Equipee, nightVision, comp.PreviousNightVision);
        comp.PreviousNightVision = null;
    }

    private void ApplyPnvNightVision(EntityUid equipee, PNVComponent pnv, NightVisionComponent nightVision, PreviousNightVisionState previous)
    {
        if (nightVision.ActionToggleNightVisionEntity is { } previousAction &&
            TryComp<ActionComponent>(previousAction, out var previousActionComp))
        {
            previous.ActionCooldownRemaining = GetCooldownRemaining(previousActionComp);
            previous.ActionWasTemporary = previousActionComp.Temporary;
            _actions.RemoveCooldown((previousAction, previousActionComp));
            _actions.SetTemporary((previousAction, previousActionComp), false);
        }

        _actions.RemoveAction(equipee, nightVision.ActionToggleNightVisionEntity);
        nightVision.ActionToggleNightVisionEntity = null;

        var pnvNightVision = new NightVisionComponent(pnv.Color, pnv.ActivateSound, pnv.Animation, pnv.Desaturation);
        nightVision.Color = pnvNightVision.Color;
        nightVision.ActivateSound = pnvNightVision.ActivateSound;
        nightVision.Duration = pnvNightVision.Duration;
        nightVision.Animation = pnvNightVision.Animation;
        nightVision.Desaturation = pnvNightVision.Desaturation;
        nightVision.ActionToggleNightVision = pnv.ActionToggleNightVision;
        nightVision.IsNightVision = false;
        nightVision.RemainingTime = null;

        _actions.AddAction(equipee, ref nightVision.ActionToggleNightVisionEntity, nightVision.ActionToggleNightVision);
        Dirty(equipee, nightVision);
    }

    private void RestorePreviousNightVision(EntityUid equipee, NightVisionComponent nightVision, PreviousNightVisionState previous)
    {
        if (nightVision.ActionToggleNightVisionEntity is { } pnvAction)
        {
            _actions.RemoveAction(equipee, pnvAction);
            QueueDel(pnvAction);
        }

        nightVision.Color = previous.Color;
        nightVision.ActivateSound = previous.ActivateSound;
        nightVision.Duration = previous.Duration;
        nightVision.Animation = previous.Animation;
        nightVision.Desaturation = previous.Desaturation;
        nightVision.ActionToggleNightVision = previous.ActionToggleNightVision;
        nightVision.ActionToggleNightVisionEntity = previous.ActionToggleNightVisionEntity;
        nightVision.IsNightVision = false;
        nightVision.RemainingTime = null;

        _actions.AddAction(equipee, ref nightVision.ActionToggleNightVisionEntity, nightVision.ActionToggleNightVision);
        RestoreActionState(nightVision.ActionToggleNightVisionEntity, previous);
        Dirty(equipee, nightVision);
    }

    private TimeSpan? GetCooldownRemaining(ActionComponent action)
    {
        if (action.Cooldown is not { } cooldown)
            return null;

        var remaining = cooldown.End - _timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private void RestoreActionState(EntityUid? actionUid, PreviousNightVisionState previous)
    {
        if (actionUid == null ||
            !TryComp<ActionComponent>(actionUid, out var action))
        {
            return;
        }

        if (previous.ActionCooldownRemaining is { } remaining)
            _actions.SetCooldown((actionUid.Value, action), remaining);
        else
            _actions.RemoveCooldown((actionUid.Value, action));

        if (previous.ActionWasTemporary is { } temporary)
            _actions.SetTemporary((actionUid.Value, action), temporary);
    }
}
