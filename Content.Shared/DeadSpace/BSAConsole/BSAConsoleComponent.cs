// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.DeadSpace.BSAConsole;

[RegisterComponent]
public sealed partial class BSAConsoleComponent : Component
{
    public const string DiskSlotId = "BSAConsole-DiskSlot";

    [DataField]
    public ItemSlot DiskSlot = new();

    public EntityUid? LinkedBSA;
    public BSAConsoleViewMode CurrentViewMode = BSAConsoleViewMode.MassScannerLocal;

    public EntityUid? TargetMapUid;
    public string? TargetMapName;
    public bool HasDisk;

    public string? SelectedGridName;
    public EntityUid? SelectedGridUid;

    public TimeSpan NextUiUpdate;
}
