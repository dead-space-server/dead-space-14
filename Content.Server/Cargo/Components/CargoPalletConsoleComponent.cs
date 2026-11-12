using Content.Server.Cargo.Systems;
using Content.Shared.Stacks;

namespace Content.Server.Cargo.Components;

[RegisterComponent]
[Access(typeof(CargoSystem))]
public sealed partial class CargoPalletConsoleComponent : Component
{
    // DS14-start
    [DataField]
    public bool GiveOutMoney;

    [DataField]
    public bool IsTaipan;
    // DS14-end
}