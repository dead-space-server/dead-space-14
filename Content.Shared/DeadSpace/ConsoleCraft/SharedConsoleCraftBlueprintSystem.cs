// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.ConsoleCraft;

public abstract partial class SharedConsoleCraftBlueprintSystem : EntitySystem
{
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected IPrototypeManager Proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsoleCraftBlueprintReceiverComponent, ComponentStartup>(OnReceiverStartup);
        SubscribeLocalEvent<ConsoleCraftBlueprintReceiverComponent, AfterInteractUsingEvent>(OnAfterInteract);
    }

    private void OnReceiverStartup(EntityUid uid, ConsoleCraftBlueprintReceiverComponent comp, ComponentStartup args)
    {
        Container.EnsureContainer<Container>(uid, ConsoleCraftBlueprintReceiverComponent.ContainerId);
    }

    private void OnAfterInteract(EntityUid uid, ConsoleCraftBlueprintReceiverComponent comp,
        ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<ConsoleCraftBlueprintComponent>(args.Used, out var blueprintComp))
            return;

        args.Handled = TryInsertBlueprint(
            (uid, comp),
            (args.Used, blueprintComp),
            args.User);
    }

    public bool TryInsertBlueprint(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        Entity<ConsoleCraftBlueprintComponent> blueprint,
        EntityUid? user)
    {
        if (!CanInsertBlueprint(receiver, blueprint, user))
            return false;

        var bpContainer = Container.GetContainer(receiver, ConsoleCraftBlueprintReceiverComponent.ContainerId);
        if (!Container.Insert(blueprint.Owner, bpContainer))
            return false;

        Popup.PopupPredicted(
            Loc.GetString("consolecraft-blueprint-inserted",
                ("recipe", blueprint.Comp.Recipe.Id)),
            receiver, user);

        OnBlueprintInserted(receiver, blueprint);
        return true;
    }

    public IEnumerable<Entity<ConsoleCraftBlueprintComponent>> GetLoadedBlueprints(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver)
    {
        var bpContainer = Container.GetContainer(receiver, ConsoleCraftBlueprintReceiverComponent.ContainerId);
        foreach (var ent in bpContainer.ContainedEntities)
        {
            if (TryComp<ConsoleCraftBlueprintComponent>(ent, out var bp))
                yield return (ent, bp);
        }
    }

    public HashSet<string> GetAvailableRecipeIds(Entity<ConsoleCraftBlueprintReceiverComponent> receiver)
    {
        var set = new HashSet<string>();
        foreach (var bp in GetLoadedBlueprints(receiver))
            set.Add(bp.Comp.Recipe.Id);
        return set;
    }

    public bool CanInsertBlueprint(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        Entity<ConsoleCraftBlueprintComponent> blueprint,
        EntityUid? user)
    {
        if (!Proto.HasIndex<ConsoleCraftPrototype>(blueprint.Comp.Recipe))
        {
            Log.Error($"Blueprint {ToPrettyString(blueprint)} references unknown recipe '{blueprint.Comp.Recipe}'.");
            return false;
        }

        var comp = receiver.Comp;
        if (comp.MaxBlueprints > 0)
        {
            var bpContainer = Container.GetContainer(receiver, ConsoleCraftBlueprintReceiverComponent.ContainerId);
            if (bpContainer.ContainedEntities.Count >= comp.MaxBlueprints)
            {
                Popup.PopupPredicted(
                    Loc.GetString("consolecraft-blueprint-slots-full"),
                    receiver, user);
                return false;
            }
        }

        foreach (var loaded in GetLoadedBlueprints(receiver))
        {
            if (loaded.Comp.Recipe == blueprint.Comp.Recipe)
            {
                Popup.PopupPredicted(
                    Loc.GetString("consolecraft-blueprint-already-loaded"),
                    receiver, user);
                return false;
            }
        }

        return Container.CanInsert(blueprint,
            Container.GetContainer(receiver, ConsoleCraftBlueprintReceiverComponent.ContainerId));
    }

    protected virtual void OnBlueprintInserted(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        Entity<ConsoleCraftBlueprintComponent> blueprint) { }
}
