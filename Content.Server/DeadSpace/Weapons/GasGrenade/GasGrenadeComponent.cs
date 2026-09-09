// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.GasGrenade;

namespace Content.Server.DeadSpace.Weapons.GasGrenade;

[RegisterComponent]
public sealed partial class GasGrenadeComponent : Component
{
    [DataField]
    public List<string> SlotIds = new() { "shell1", "shell2" };

    [DataField]
    public GasGrenadeMode Mode = GasGrenadeMode.Mix;

    [DataField]
    public HashSet<string> KeysIn = new() { "timer" };

    [DataField]
    public float MixDelay = 1f;

    [DataField]
    public float MixReactInterval = 0.25f;

    [ViewVariables]
    public float MixReactTimer;

    [DataField]
    public string ReleaseSound = "/Audio/Effects/spray.ogg";

    [ViewVariables]
    public float? MixCountdown;

    [ViewVariables]
    public bool Releasing;

    [ViewVariables]
    public EntityUid? MixShellEntity;
}
