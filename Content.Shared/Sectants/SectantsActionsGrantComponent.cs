using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Sectants;

/// <summary>Выдаёт набор экшенов при инициализации сущности.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SectantActionsGrantComponent : Component
{
    [DataField]
    public EntProtoId[] Actions = Array.Empty<EntProtoId>();
}
