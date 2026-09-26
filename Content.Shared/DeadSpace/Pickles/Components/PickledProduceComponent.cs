// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Pickles;
using Robust.Shared.GameStates;

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
}
