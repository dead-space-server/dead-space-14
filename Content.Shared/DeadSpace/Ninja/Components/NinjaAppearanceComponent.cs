using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

public enum NinjaColorway : byte
{
    Red = 0,
    Blue = 1,
    Green = 2,
}

public enum NinjaStyle : byte
{
    Old = 0,
    New = 1,
}

public enum NinjaAppearanceItemType : byte
{
    Color = 0,

    Scarf = 1,

    Helmet = 2,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedNinjaAppearanceSystem))]
public sealed partial class NinjaAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaColorway Colorway = NinjaColorway.Green;

    [DataField, AutoNetworkedField]
    public NinjaStyle Style = NinjaStyle.Old;

    [DataField, AutoNetworkedField]
    public bool ScarfShown = false;

    [DataField, AutoNetworkedField]
    public bool HelmetShown = true;
}