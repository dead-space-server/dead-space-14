namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Component used for allowing non-humans to be converted. (Mainly monkeys)
/// </summary>
<<<<<<< ours
[RegisterComponent, Access(typeof(SharedRevolutionarySystem))] // DS14: server-only marker; roster visibility is sent separately.
||||||| base
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
=======
[RegisterComponent, NetworkedComponent, Access(typeof(RevolutionarySystem))]
>>>>>>> theirs
public sealed partial class AlwaysRevolutionaryConvertibleComponent : Component
{

}
