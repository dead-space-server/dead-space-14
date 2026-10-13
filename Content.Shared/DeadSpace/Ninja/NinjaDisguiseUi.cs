// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[NetSerializable, Serializable]
public enum NinjaDisguiseUiKey : byte
{
    Key
}

[NetSerializable, Serializable]
public sealed class NinjaDisguiseState : BoundUserInterfaceState
{
    public List<NinjaDisguiseEntry> Entries { get; }
    public bool Disguised { get; }
    public int? ActiveIndex { get; }

    public NinjaDisguiseState(List<NinjaDisguiseEntry> entries, bool disguised, int? activeIndex)
    {
        Entries = entries;
        Disguised = disguised;
        ActiveIndex = activeIndex;
    }
}

[NetSerializable, Serializable]
public sealed class NinjaDisguiseApplyMessage : BoundUserInterfaceMessage
{
    public int Index { get; }

    public NinjaDisguiseApplyMessage(int index)
    {
        Index = index;
    }
}

[NetSerializable, Serializable]
public sealed class NinjaDisguiseResetMessage : BoundUserInterfaceMessage { }