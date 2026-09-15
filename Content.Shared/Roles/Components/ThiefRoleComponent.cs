using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Added to mind role entities to tag that they are a thief.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThiefRoleComponent : BaseMindRoleComponent
{
    /// <summary>
    /// DS14: Сколько воришка заработал грязных кредитов
    /// Серверсайд онли, для цели и манифеста.
    /// </summary>
    [DataField]
    public int CreditsEarned;
}
