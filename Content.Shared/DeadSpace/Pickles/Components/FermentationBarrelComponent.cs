// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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

    /// <summary>Finished batch method — vinegar/salt produce vs alcohol drink.</summary>
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
