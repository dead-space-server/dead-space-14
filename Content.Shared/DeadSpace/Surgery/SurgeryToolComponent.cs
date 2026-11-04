// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public enum SurgeryToolQuality : byte
{
    Incision,

    Extraction,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public SurgeryToolQuality Quality = SurgeryToolQuality.Incision;

    [DataField]
    public SoundPathSpecifier? IncisionSound;

    [DataField]
    public SoundPathSpecifier? ExtractSound;

    [DataField]
    public SoundPathSpecifier? InsertSound;
}
