using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAshWalkerNestSystem))]
public sealed partial class AshWalkerNestComponent : Component
{
    [DataField]
    public EntityUid? HomeMap;

    [DataField]
    public int Flesh;

    [DataField]
    public int FleshCapacity = 20;

    [DataField]
    public int EggCost = 2;

    [DataField]
    public int MaxEggs = 6;

    [DataField]
    public int HumanoidValue = 1;

    [DataField]
    public Dictionary<EntProtoId, int> SacrificeValues = new();

    [DataField]
    public TimeSpan SacrificeDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan IncubationTime = TimeSpan.FromSeconds(60);

    [DataField]
    public EntProtoId EggPrototype = "AshWalkerEggIncubating";

    [DataField]
    public EntProtoId CollapsePrototype = "AshWalkerNestCollapse";

    [DataField]
    public DamageSpecifier Healing = new();

    [DataField]
    public SoundSpecifier SacrificeSound = new SoundCollectionSpecifier("gib");

    [DataField]
    public bool Destroyed;
}
