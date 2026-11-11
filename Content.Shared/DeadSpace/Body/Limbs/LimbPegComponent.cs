// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent]
public sealed partial class LimbPegComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Left;

    [DataField(required: true)]
    public EntProtoId Right;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");
}

[RegisterComponent, NetworkedComponent]
public sealed partial class LimbProsthesisComponent : Component
{
    [DataField(required: true)]
    public ProtoId<HumanoidSpeciesSpriteLayer> Sprite;
}

[Serializable, NetSerializable]
public sealed partial class LimbPegDoAfterEvent : SimpleDoAfterEvent;
