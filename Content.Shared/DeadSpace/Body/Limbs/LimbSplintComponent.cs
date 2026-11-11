// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent]
public sealed partial class LimbSplintComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan SelfDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan Knit = TimeSpan.FromMinutes(3);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");
}

[RegisterComponent]
public sealed partial class LimbKnittingComponent : Component
{
    [DataField]
    public Dictionary<LimbZone, TimeSpan> Due = new();

    [DataField]
    public TimeSpan Knit = TimeSpan.FromMinutes(3);
}

[Serializable, NetSerializable]
public sealed partial class LimbSplintDoAfterEvent : SimpleDoAfterEvent;
