// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Pickles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PickleJarComponent : Component
{
    public const string SolutionName = "drink";

    [DataField, AutoNetworkedField]
    public EntProtoId? PiecePrototype;

    [DataField, AutoNetworkedField]
    public int RemainingPieces;

    [DataField, AutoNetworkedField]
    public int MaxPieces = 4;

    [DataField, AutoNetworkedField]
    public Color? PieceTint;

    [DataField, AutoNetworkedField]
    public PickleMethod Method = PickleMethod.Vinegar;

    [DataField, AutoNetworkedField]
    public bool IsWine;

    public const string DefaultContentsStyle = "cucumber";

    [DataField, AutoNetworkedField]
    public string ContentsStyle = DefaultContentsStyle;

    [DataField]
    public float SlipChance = 0.25f;

    [DataField]
    public float WineBlindChance = 0.05f;

    [DataField]
    public float SlipSpill = 8f;

    [DataField]
    public float PieceShare = 4f;

    [DataField]
    public LocId PieceName = "pickle-piece-pickled";
}

[Serializable, NetSerializable]
public enum PickleJarVisuals : byte
{
    ProduceCount,
    HasProduce,
    ContentsStyle,
}
