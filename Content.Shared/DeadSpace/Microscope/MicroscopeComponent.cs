// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Microscope;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MicroscopeComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SlotId = "microscope_organ_slot";

    [DataField]
    public SoundPathSpecifier? InsertSound;

    [DataField]
    public SoundPathSpecifier? EjectSound;
}
