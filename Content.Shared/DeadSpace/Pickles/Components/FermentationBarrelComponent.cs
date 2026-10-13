// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Pickles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FermentationBarrelComponent : Component
{
    public const string ProduceContainerId = "pickle_produce";
    public const string SolutionName = "tank";

    [DataField, AutoNetworkedField]
    public FermentationState State = FermentationState.Idle;

    [DataField, AutoNetworkedField]
    public PickleMethod? ReadyMethod;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? ReadyDrinkReagent;

    [DataField]
    public int MaxProduce = 6;

    [DataField, AutoNetworkedField]
    public TimeSpan Elapsed = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan TargetDuration = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan InvalidTempTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan NextFart = TimeSpan.Zero;

    [DataField]
    public float MinTemperature = 283.15f;

    [DataField]
    public float MaxTemperature = 313.15f;

    [DataField]
    public float BacteriaSpeedMultiplier = 3f;

    [DataField]
    public float BacteriaFartMoles = 0.4f;

    [DataField]
    public SoundSpecifier FartSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    [DataField]
    public SoundSpecifier CompleteSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    [DataField]
    public SoundSpecifier BurstSound = new SoundCollectionSpecifier("GlassBreak");

    [DataField]
    public EntProtoId ShardPrototype = "ShardGlass";

    [DataField]
    public ProtoId<ReagentPrototype> VinegarReagent = "Vinegar";

    [DataField]
    public ProtoId<ReagentPrototype> SaltReagent = "TableSalt";

    [DataField]
    public ProtoId<ReagentPrototype> SugarReagent = "Sugar";

    [DataField]
    public ProtoId<ReagentPrototype> WaterReagent = "Water";

    [DataField]
    public ProtoId<ReagentPrototype> BacteriaReagent = "PickleBacteria";

    [DataField]
    public ProtoId<ReagentPrototype> VinegarBrineReagent = "PickleVinegarBrine";

    [DataField]
    public ProtoId<ReagentPrototype> SaltBrineReagent = "PickleSaltBrine";

    [DataField]
    public ProtoId<ReagentPrototype> NutrimentReagent = "Nutriment";

    [DataField]
    public ProtoId<ReagentPrototype> FallbackDrink = "PickleWine";

    [DataField]
    public LocId ReadyPieceName = "pickle-piece-wine";

    [DataField]
    public float FartInterval = 5f;

    [DataField]
    public float HotFailSeconds = 5f;

    [DataField]
    public float ColdFailSeconds = 25f;

    [DataField]
    public float BacteriaSpeedAt = 5f;

    [DataField]
    public float BacteriaBurstAt = 40f;

    [DataField]
    public float DrinkPerJar = 80f;

    [DataField]
    public float BrinePerPiece = 6f;

    [DataField]
    public float BrineMinimum = 8f;

    [DataField]
    public float LowBrinePerPiece = 2f;

    [DataField]
    public float NutrimentPerPiece = 2f;

    [DataField]
    public float ExtraShardChance = 0.5f;
}

[Serializable, NetSerializable]
public enum FermentationState : byte
{
    Idle,
    Fermenting,
    Ready,
}

[Serializable, NetSerializable]
public enum FermentationVisuals : byte
{
    State,
    Layer,
}
