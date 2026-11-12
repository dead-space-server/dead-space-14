using Content.Shared.Station.Systems;

namespace Content.Shared.Station.Components;

/// <summary>
/// Controls spawning on the given station, tracking spawners present on it.
/// </summary>
[RegisterComponent, Access(typeof(StationSpawningSystem))]
<<<<<<< ours
public sealed partial class StationSpawningComponent : Component
{
    [DataField]
    public bool AllowRandomSpawn = true;
}
||||||| base
public sealed partial class StationSpawningComponent : Component
{
}
=======
public sealed partial class StationSpawningComponent : Component;
>>>>>>> theirs
