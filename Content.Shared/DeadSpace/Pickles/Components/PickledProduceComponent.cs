// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Pickles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Pickles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PickledProduceComponent : Component
{
    [DataField, AutoNetworkedField]
    public PickleMethod Method = PickleMethod.Vinegar;

    [DataField, AutoNetworkedField]
    public Color Tint = Color.FromHex("#e8d070");

    [DataField, AutoNetworkedField]
    public LocId PieceName = "pickle-piece-pickled";

    [DataField, AutoNetworkedField]
    public float SpriteScale = 0.65f;

    [DataField]
    public string FoodSolution = "food";

    [DataField]
    public float FoodMaxVolume = 40f;

    [DataField]
    public ProtoId<ReagentPrototype> WaterReagent = "Water";

    [DataField]
    public List<ProtoId<ReagentPrototype>> FlavorIgnore = new()
    {
        "Water",
        "Nutriment",
        "Vitamin",
    };
}
