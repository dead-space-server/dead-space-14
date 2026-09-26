using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Smokables;

/// <summary>A portable reservoir owning one permanently connected hose.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShishaComponent : Component
{
    public const string HoseContainer = "shisha_hose";

    [DataField, AutoNetworkedField]
    public EntityUid? Hose;

    /// <summary>Prevents a saved base with a destroyed hose from manufacturing a replacement.</summary>
    [DataField]
    public bool HoseInitialized;

    [DataField]
    public EntProtoId HosePrototype = "ShishaHose";

    [DataField]
    public string Solution = "shisha";

    [DataField]
    public ProtoId<StackPrototype> Fuel = "Coal";

    /// <summary>One piece of coal supplies this many seconds of heat.</summary>
    [DataField]
    public float FuelPerItem = 300f;

    [DataField]
    public float FuelRemaining;

    [DataField]
    public bool Lit;

    [DataField]
    public SoundSpecifier LightSound = new SoundPathSpecifier("/Audio/Effects/cig_light.ogg");

    [DataField]
    public float HoseLength = 0.25f;

    [DataField]
    public FixedPoint2 Dose = FixedPoint2.New(6);

    [DataField]
    public float HoseSag = 0.15f;

    [DataField]
    public TimeSpan PuffDuration = TimeSpan.FromSeconds(2);

    /// <summary>Presentation assets can be replaced in the prototype without changing the smoking logic.</summary>
    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("_SS220/Objects/Specific/Hookah/hookah_rope.rsi"), "rope");

    [DataField]
    public EntProtoId PuffPrototype = "ShishaPuff";

    [DataField]
    public SoundSpecifier PuffSound = new SoundPathSpecifier("/Audio/Effects/cig_snuff.ogg");

    [DataField]
    public SoundSpecifier EmptyPuffSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");
}
