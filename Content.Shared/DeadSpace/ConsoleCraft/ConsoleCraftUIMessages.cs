// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.ConsoleCraft;

[Serializable, NetSerializable]
public enum ConsoleCraftUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class ConsoleCraftRequirementStatus
{
    public string Label { get; init; } = string.Empty;
    public string ProtoId { get; init; } = string.Empty;
    public int Required { get; init; }
    public int Inserted { get; init; }
    public bool Satisfied => Inserted >= Required;
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftModuleStatus
{
    public string ModuleId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Inserted { get; init; }
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftBlueprintEntry
{
    public string RecipeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? RemainingCrafts { get; init; }
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftItemStat
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftConsoleState : BoundUserInterfaceState
{
    public List<ConsoleCraftBlueprintEntry> AvailableRecipes { get; init; } = new();
    public string? SelectedRecipeId { get; init; }
    public string? CraftItemProtoId { get; init; }
    public List<ConsoleCraftRequirementStatus> RequiredStatus { get; init; } = new();
    public List<ConsoleCraftModuleStatus> ModuleStatus { get; init; } = new();
    public List<ConsoleCraftItemStat> ItemStats { get; init; } = new();
    public bool CanCraft { get; init; }
    public bool CraftInProgress { get; init; }
    public bool NoStation { get; init; }
    public int? SelectedBlueprintRemainingCrafts { get; init; }
    public bool CanDismantle { get; set; } = false;
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftSelectBlueprintMessage : BoundUserInterfaceMessage
{
    public string RecipeId { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ConsoleCraftBackMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ConsoleCraftStartMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ConsoleCraftEjectMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ConsoleCraftDismantleMessage : BoundUserInterfaceMessage { }
