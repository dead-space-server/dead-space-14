using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Prison;

[Serializable, NetSerializable]
public sealed class PrisonFactionEuiState : EuiStateBase
{
    public List<PrisonFactionOption> Factions { get; }
    public int SecondsRemaining { get; }

    public PrisonFactionEuiState(List<PrisonFactionOption> factions, int secondsRemaining)
    {
        Factions = factions;
        SecondsRemaining = secondsRemaining;
    }
}

[Serializable, NetSerializable]
public sealed class PrisonFactionOption
{
    public string Id { get; }
    public LocId Name { get; }
    public LocId Feature { get; }
    public Color Color { get; }

    public PrisonFactionOption(
        string id,
        LocId name,
        LocId feature,
        Color color)
    {
        Id = id;
        Name = name;
        Feature = feature;
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class PrisonFactionSelectedMessage : EuiMessageBase
{
    public string FactionId { get; }

    public PrisonFactionSelectedMessage(string factionId)
    {
        FactionId = factionId;
    }
}
