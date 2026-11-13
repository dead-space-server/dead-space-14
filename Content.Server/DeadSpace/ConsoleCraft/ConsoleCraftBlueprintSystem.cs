// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ConsoleCraft;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.ConsoleCraft;

public sealed partial class ConsoleCraftBlueprintSystem : SharedConsoleCraftBlueprintSystem
{
    [Dependency] private ConsoleCraftSystem _craftSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    protected override void OnBlueprintInserted(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        Entity<ConsoleCraftBlueprintComponent> blueprint)
    {
        if (TryComp<ConsoleCraftConsoleComponent>(receiver, out var consoleComp))
            _craftSystem.RefreshConsoleState(receiver, consoleComp);
    }

    public bool RecordCraftUse(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        string recipeId)
    {
        foreach (var bp in GetLoadedBlueprints(receiver))
        {
            if (bp.Comp.Recipe.Id != recipeId)
                continue;

            if (bp.Comp.MaxCrafts.HasValue)
            {
                bp.Comp.CraftsUsed++;

                if (bp.Comp.IsExhausted)
                    EjectBlueprint(receiver, bp);
                else
                {
                    if (TryComp<ConsoleCraftConsoleComponent>(receiver, out var con))
                        _craftSystem.RefreshConsoleState(receiver, con);
                }
            }

            return true;
        }

        return false;
    }

    private void EjectBlueprint(
        Entity<ConsoleCraftBlueprintReceiverComponent> receiver,
        Entity<ConsoleCraftBlueprintComponent> blueprint)
    {
        var bpContainer = Container.GetContainer(receiver, ConsoleCraftBlueprintReceiverComponent.ContainerId);
        Container.Remove(blueprint.Owner, bpContainer, reparent: false, force: true);
        Del(blueprint.Owner);

        var recipeName = _proto.TryIndex<ConsoleCraftPrototype>(blueprint.Comp.Recipe.Id, out var recipeDef)
            ? recipeDef.Name
            : blueprint.Comp.Recipe.Id;

        Popup.PopupEntity(
            Loc.GetString("consolecraft-blueprint-exhausted",
                ("recipe", recipeName)),
            receiver);

        if (TryComp<ConsoleCraftConsoleComponent>(receiver, out var consoleComp) &&
            consoleComp.SelectedBlueprintId == blueprint.Comp.Recipe.Id)
        {
            consoleComp.SelectedBlueprintId = null;
        }

        if (TryComp<ConsoleCraftConsoleComponent>(receiver, out var con))
            _craftSystem.RefreshConsoleState(receiver, con);
    }
}