// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Attachments;

[Serializable, NetSerializable]
public enum AttachmentUiKey : byte
{
    Key,
}

/// <summary>One slot of the attachment menu.</summary>
[Serializable, NetSerializable]
public sealed class AttachmentSlotState(string slotId, string name, string? item, NetEntity? itemEntity, bool canInsert)
{
    public readonly string SlotId = slotId;
    public readonly string Name = name;
    public readonly string? Item = item;
    public readonly NetEntity? ItemEntity = itemEntity;

    /// <summary>The item in the user's active hand fits this slot.</summary>
    public readonly bool CanInsert = canInsert;
}

[Serializable, NetSerializable]
public sealed class AttachmentHolderBuiState(List<AttachmentSlotState> slots) : BoundUserInterfaceState
{
    public readonly List<AttachmentSlotState> Slots = slots;
}

/// <summary>Put the item from the active hand into the slot.</summary>
[Serializable, NetSerializable]
public sealed class AttachmentInsertMessage(string slotId) : BoundUserInterfaceMessage
{
    public readonly string SlotId = slotId;
}

/// <summary>Take the item out of the slot into the hands.</summary>
[Serializable, NetSerializable]
public sealed class AttachmentEjectMessage(string slotId) : BoundUserInterfaceMessage
{
    public readonly string SlotId = slotId;
}

/// <summary>Action of a weapon with a flashlight slot: turns the attached flashlight on and off.</summary>
public sealed partial class AttachmentToggleLightActionEvent : InstantActionEvent;
