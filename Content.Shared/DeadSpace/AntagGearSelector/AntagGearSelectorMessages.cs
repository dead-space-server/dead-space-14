// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AntagGearSelector;

[Serializable, NetSerializable]
public sealed class AntagGearSelectorEuiState : EuiStateBase
{
    public List<AntagGearSelectorOption> Gear { get; }
    public TimeSpan Deadline { get; }

    public AntagGearSelectorEuiState(List<AntagGearSelectorOption> gear, TimeSpan deadline)
    {
        Gear = gear;
        Deadline = deadline;
    }
}

[Serializable, NetSerializable]
public sealed class AntagGearSelectorOption
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SpritePrototype { get; set; } = string.Empty;
    public List<AntagGearSelectorPerkOption> Perks { get; set; } = [];

    public AntagGearSelectorOption()
    {
    }

    public AntagGearSelectorOption(int index, string name, string description, string spritePrototype)
    {
        Index = index;
        Name = name;
        Description = description;
        SpritePrototype = spritePrototype;
    }
}

[Serializable, NetSerializable]
public sealed class AntagGearSelectorPerkOption
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SpritePrototype { get; set; } = string.Empty;

    public AntagGearSelectorPerkOption()
    {
    }

    public AntagGearSelectorPerkOption(int index, string name, string description, string spritePrototype)
    {
        Index = index;
        Name = name;
        Description = description;
        SpritePrototype = spritePrototype;
    }
}

[Serializable, NetSerializable]
public sealed class AntagGearSelectorSelectedMessage : EuiMessageBase
{
    public int GearIndex { get; }
    public int PerkIndex { get; }

    public AntagGearSelectorSelectedMessage(int gearIndex, int perkIndex)
    {
        GearIndex = gearIndex;
        PerkIndex = perkIndex;
    }
}
