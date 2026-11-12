using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

/// <summary>
///     Component that denotes a piece of clothing that blocks the mouth or otherwise prevents eating & drinking.
/// </summary>
/// <remarks>
///     In the event that more head-wear & mask functionality is added (like identity systems, or raising/lowering of
///     masks), then this component might become redundant.
/// </remarks>
<<<<<<< ours
[RegisterComponent, Access(typeof(IngestionSystem))]
[NetworkedComponent,AutoGenerateComponentState(true)]//DS14
||||||| base
[RegisterComponent, Access(typeof(IngestionSystem))]
=======
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(IngestionSystem))]
>>>>>>> theirs
public sealed partial class IngestionBlockerComponent : Component
{
    /// <summary>
    ///     Whether this item currently blocks consuming something.
    /// </summary>
<<<<<<< ours
    [DataField]
    [AutoNetworkedField]//DS14
    public bool Enabled { get; set; } = true;
}
||||||| base
    [DataField]
    public bool Enabled { get; set; } = true;
}
=======
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
>>>>>>> theirs
