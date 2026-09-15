using Content.Shared.Clothing.Components;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Clothing.EntitySystems;

public sealed class FleetingClothingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleetingClothingComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<FleetingClothingComponent, BeforeGettingUnequippedEvent>(OnBeforeGettingUnequipped);
        SubscribeLocalEvent<FleetingClothingComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnExamine(Entity<FleetingClothingComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        string? examineText;
        if (TryComp<ClothingComponent>(ent, out var clothing) &&
            clothing.InSlot != null &&
            Transform(ent).ParentUid == args.Examiner)
        {
            examineText = ent.Comp.ExamineWearer == null ? null : Loc.GetString(ent.Comp.ExamineWearer);
        }
        else
        {
            examineText = ent.Comp.ExamineOthers == null ? null : Loc.GetString(ent.Comp.ExamineOthers);
        }

        if (!string.IsNullOrEmpty(examineText))
            args.PushMarkup(examineText);
    }

    private void OnBeforeGettingUnequipped(Entity<FleetingClothingComponent> ent, ref BeforeGettingUnequippedEvent args)
    {
        Remove(ent);

        var coordinates = Transform(ent).Coordinates;
        if (ent.Comp.PlaySoundOnSelfUnequip || args.User != args.EquipTarget)
            _audio.PlayPredicted(ent.Comp.RemovedSound, coordinates, args.User);

        if (args.User == args.EquipTarget)
        {
            var selfMessage = ent.Comp.SelfUnquipPopupWearer == null
                ? null
                : Loc.GetString(ent.Comp.SelfUnquipPopupWearer, ("item", ent.Owner));
            var othersMessage = ent.Comp.SelfUnquipPopupOthers == null
                ? null
                : Loc.GetString(ent.Comp.SelfUnquipPopupOthers, ("item", ent.Owner));
            _popup.PopupPredicted(selfMessage, othersMessage, args.EquipTarget, args.User, PopupType.LargeCaution);
        }
        else if (ent.Comp.RemovedPopup != null)
        {
            _popup.PopupPredicted(
                Loc.GetString(ent.Comp.RemovedPopup, ("item", ent.Owner)),
                args.EquipTarget,
                args.User,
                PopupType.LargeCaution);
        }
    }

    private void OnGotUnequipped(Entity<FleetingClothingComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState || Terminating(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        if (Terminating(args.Equipee))
            return;

        Remove(ent);

        if (_net.IsClient)
            return;

        _audio.PlayPvs(ent.Comp.RemovedSound, args.Equipee);
        if (ent.Comp.RemovedPopup != null)
        {
            _popup.PopupEntity(
                Loc.GetString(ent.Comp.RemovedPopup, ("item", ent.Owner)),
                args.Equipee,
                PopupType.LargeCaution);
        }
    }

    private void Remove(Entity<FleetingClothingComponent> ent)
    {
        if (ent.Comp.DestroyOnUnequip)
            _destructible.DestroyEntity(ent.Owner);
        else
            PredictedQueueDel(ent.Owner);
    }
}
