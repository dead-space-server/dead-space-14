// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Psychiatry;

[Prototype]
public sealed partial class PsychiatryScarePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    [DataField]
    public string? FallbackState;

    [DataField]
    public SpriteSpecifier? FallbackSprite;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Speed = 1.6f;

    [DataField]
    public float Duration = 1.4f;

    [DataField]
    public Vector2 Size = Vector2.One;

    [DataField]
    public int Weight = 1;

    [DataField]
    public bool Wobble;

    [DataField]
    public bool Rotate;
}
