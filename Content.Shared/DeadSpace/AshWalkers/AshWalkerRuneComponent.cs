using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AshWalkers;

[Serializable, NetSerializable]
public enum AshWalkerRite : byte
{
    Hunt,
    Mending,
    Return,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AshWalkerRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? HomeMap;

    [DataField]
    public DoAfterId? Invocation;

    [DataField]
    public AshWalkerRite Rite;

    [DataField]
    public HashSet<EntProtoId> Sacrifices = new();

    [DataField]
    public int SacrificeCount = 1;

    [DataField]
    public TimeSpan RitualTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan EffectDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public EntProtoId? Effect;
}
