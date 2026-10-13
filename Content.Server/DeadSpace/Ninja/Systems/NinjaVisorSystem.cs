// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.NightVision;
using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaVisorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NinjaVisorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaVisorComponent, CycleNinjaVisorActionEvent>(OnCycle);
        SubscribeLocalEvent<NinjaVisorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaVisorComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<NinjaVisorComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<NinjaVisorComponent, ComponentShutdown>(OnVisorShutdown);
    }

    private void OnMapInit(Entity<NinjaVisorComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.CycleActionEntity, ent.Comp.CycleAction);
        Dirty(ent);
        ApplyFlashImmunity(ent);
    }

    private void OnGetActions(Entity<NinjaVisorComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;
        args.AddAction(ent.Comp.CycleActionEntity);
    }

    private void OnCycle(Entity<NinjaVisorComponent> ent, ref CycleNinjaVisorActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetMode(ent, Next(ent.Comp.Mode));
    }

    private void OnGotEquipped(Entity<NinjaVisorComponent> ent, ref GotEquippedEvent args)
    {
        ApplyFlashImmunity(ent);
        if (args.Equipee.IsValid())
            ApplyWearerEffect(ent, args.Equipee);
    }

    private void OnGotUnequipped(Entity<NinjaVisorComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.AppliedEffect is { } effect)
        {
            ClearEffect(args.Equipee, effect);
            ent.Comp.AppliedEffect = null;
            ent.Comp.AppliedWearer = null;
            Dirty(ent);
        }
    }

    private void OnVisorShutdown(Entity<NinjaVisorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AppliedEffect is { } effect && ent.Comp.AppliedWearer is { } wearer && Exists(wearer))
            ClearEffect(wearer, effect);
    }

    private static NinjaVisorMode Next(NinjaVisorMode mode) => mode switch
    {
        NinjaVisorMode.Protective => NinjaVisorMode.Thermal,
        NinjaVisorMode.Thermal => NinjaVisorMode.NightVision,
        _ => NinjaVisorMode.Protective,
    };

    private void SetMode(Entity<NinjaVisorComponent> ent, NinjaVisorMode mode)
    {
        if (ent.Comp.Mode == mode)
            return;

        ent.Comp.Mode = mode;
        Dirty(ent);

        if (ent.Comp.CycleSound is { } sound)
            _audio.PlayPvs(sound, ent.Owner);

        ApplyFlashImmunity(ent);

        var wearer = GetWornWearer(ent);
        if (wearer != null)
            ApplyWearerEffect(ent, wearer.Value);
    }

    private void ApplyFlashImmunity(Entity<NinjaVisorComponent> ent)
    {
        if (ent.Comp.Mode == NinjaVisorMode.Protective)
            EnsureComp<FlashImmunityComponent>(ent.Owner);
        else
            RemComp<FlashImmunityComponent>(ent.Owner);
    }

    private EntityUid? GetWornWearer(Entity<NinjaVisorComponent> ent)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid() || wearer == ent.Owner || !IsWorn(ent.Owner, wearer))
            return null;

        return wearer;
    }

    private bool IsWorn(EntityUid visor, EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return false;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (item == visor)
                return true;
        }

        return false;
    }

    private void ApplyWearerEffect(Entity<NinjaVisorComponent> ent, EntityUid wearer)
    {
        if (ent.Comp.AppliedEffect is { } previous)
        {
            ClearEffect(wearer, previous);
            ent.Comp.AppliedEffect = null;
            ent.Comp.AppliedWearer = null;
        }

        switch (ent.Comp.Mode)
        {
            case NinjaVisorMode.Thermal:
                if (!HasComp<ThermalVisionComponent>(wearer))
                {
                    var thermal = new ThermalVisionComponent
                    {
                        IsActive = true,
                        Animation = ent.Comp.ThermalAnimation,
                        UseShader = ent.Comp.ThermalUseShader,
                        ActivateSound = ent.Comp.ThermalActivateSound,
                        ActivateSoundOff = ent.Comp.ThermalActivateOffSound,
                    };
                    AddComp(wearer, thermal);
                    SuppressToggle(wearer, thermal.ActionToggleThermalVisionEntity);
                    thermal.ActionToggleThermalVisionEntity = null;
                    Dirty(wearer, thermal);
                    ent.Comp.AppliedEffect = NinjaVisorMode.Thermal;
                    ent.Comp.AppliedWearer = wearer;
                }
                break;

            case NinjaVisorMode.NightVision:
                if (!HasComp<NightVisionComponent>(wearer))
                {
                    var nv = new NightVisionComponent(
                        ent.Comp.NightVisionColor,
                        ent.Comp.NightVisionActivateSound,
                        ent.Comp.NightVisionAnimation,
                        ent.Comp.NightVisionDesaturation)
                    {
                        IsNightVision = true
                    };
                    AddComp(wearer, nv);
                    SuppressToggle(wearer, nv.ActionToggleNightVisionEntity);
                    nv.ActionToggleNightVisionEntity = null;
                    Dirty(wearer, nv);
                    ent.Comp.AppliedEffect = NinjaVisorMode.NightVision;
                    ent.Comp.AppliedWearer = wearer;
                }
                break;
        }

        Dirty(ent);
    }

    private void SuppressToggle(EntityUid wearer, EntityUid? actionEntity)
    {
        if (actionEntity is { } action)
            _actions.RemoveAction(wearer, action);
    }

    private void ClearEffect(EntityUid wearer, NinjaVisorMode? mode)
    {
        switch (mode)
        {
            case NinjaVisorMode.Thermal:
                RemComp<ThermalVisionComponent>(wearer);
                break;
            case NinjaVisorMode.NightVision:
                RemComp<NightVisionComponent>(wearer);
                break;
        }
    }
}