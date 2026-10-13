// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.DeadSpace.Ninja;

public abstract class SharedNinjaInfoScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoScannerComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<NinjaInfoScannerComponent, DragDropTargetEvent>(OnDropTarget);
    }

    private void OnCanDropTarget(
        Entity<NinjaInfoScannerComponent> ent,
        ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsScanning)
            return;

        if (!_mobState.IsAlive(args.Dragged))
            return;

        if (!_interaction.InRangeUnobstructed(
                args.User,
                ent.Owner,
                popup: false) ||
            !_interaction.InRangeUnobstructed(
                args.User,
                args.Dragged,
                popup: false))
        {
            return;
        }

        if (_container.TryGetContainer(
                ent.Owner,
                ent.Comp.ContainerId,
                out var container) &&
            container.Count == 0)
        {
            if (_container.CanInsert(args.Dragged, container))
            {
                args.CanDrop = true;
                args.Handled = true;
            }
        }
    }

    private void OnDropTarget(
        Entity<NinjaInfoScannerComponent> ent,
        ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsScanning)
            return;

        if (!_mobState.IsAlive(args.Dragged))
            return;

        if (!_interaction.InRangeUnobstructed(
                args.User,
                ent.Owner,
                popup: false) ||
            !_interaction.InRangeUnobstructed(
                args.User,
                args.Dragged,
                popup: false))
        {
            return;
        }

        if (_container.TryGetContainer(
                ent.Owner,
                ent.Comp.ContainerId,
                out var container))
        {
            if (_container.Insert(args.Dragged, container))
            {
                args.Handled = true;
            }
        }
    }
}