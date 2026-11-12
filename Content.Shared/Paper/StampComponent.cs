using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

// DS14-start
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StampComponent : Component
{
    /// <summary>
    /// Localization id or raw text printed by the stamp.
    /// </summary>
    [DataField("stampedName"), AutoNetworkedField]
    public string StampedName = "stamp-component-stamped-name-default";

    [DataField("stampedColor"), AutoNetworkedField]
    public Color StampedColor = Color.FromHex("#a23e3e");

    /// <summary>
    /// Stamp state from bureaucracy.rsi used for the small paper-in-world visual.
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string StampState = "paper_stamp-generic";

    /// <summary>
    /// Full ready-made texture drawn in the paper UI instead of the legacy bordered label.
    /// </summary>
    [DataField("stampTexture"), AutoNetworkedField]
    public string? StampTexture;

    /// <summary>
    /// Pattern texture drawn in the paper UI with text rendered over it.
    /// </summary>
    [DataField("stampPatternTexture"), AutoNetworkedField]
    public string? StampPatternTexture;

    /// <summary>
    /// Per-prototype/custom stamp scale. Kept networked so client-side prediction uses
    /// the same stamp size as the authoritative server result.
    /// </summary>
<<<<<<< ours
    [DataField("stampScale"), AutoNetworkedField]
    public float StampScale = 1.0f;

    /// <summary>
    /// Optional text drawn inside pattern stamps instead of <see cref="StampedName"/>.
    /// This lets custom stamps keep their full examine name while using shorter paper UI text.
    /// </summary>
    [DataField("stampMainText"), AutoNetworkedField]
    public string? StampMainText;

    /// <summary>
    /// Optional upper limit for the generated text scale on pattern stamps.
    /// </summary>
    [DataField("stampTextMaxScale"), AutoNetworkedField]
    public float StampTextMaxScale;

    /// <summary>
    /// Optional small text rendered in the upper strip of pattern stamps.
    /// </summary>
    [DataField("stampHeaderText"), AutoNetworkedField]
    public string? StampHeaderText;

    /// <summary>
    /// Optional background/organization text rendered in the upper strip of pattern stamps.
    /// </summary>
    [DataField("stampBackgroundText"), AutoNetworkedField]
    public string? StampBackgroundText;
||||||| base
    [DataField("stampedColor")]
    public Color StampedColor = Color.FromHex("#BB3232"); // StyleNano.DangerousRedFore
=======
    [DataField("stampedColor")]
    public Color StampedColor = Color.FromHex("#BB3232");
>>>>>>> theirs

    /// <summary>
    /// Whether this stamp is allowed to receive the random paper UI rotation.
    /// </summary>
    [DataField("stampCanRotate"), AutoNetworkedField]
    public bool StampCanRotate = true;

    /// <summary>
    /// Sound played when stamping. This intentionally stays prototype-side; visuals above
    /// are the fields that must be synchronized for prediction correctness.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Stamp/thick_stamp_sub.ogg");
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class StampDisplayInfo
{
    [DataField("stampedName")]
    public string StampedName = "stamp-component-stamped-name-default";

    [DataField("stampedColor")]
    public Color StampedColor = Color.FromHex("#a23e3e");

    [DataField("stampTexture")]
    public string? StampTexture;

    [DataField("stampPatternTexture")]
    public string? StampPatternTexture;

    [DataField("stampScale")]
    public float StampScale = 1.0f;

    [DataField("stampMainText")]
    public string? StampMainText;

    [DataField("stampTextMaxScale")]
    public float StampTextMaxScale;

    [DataField("stampHeaderText")]
    public string? StampHeaderText;

    [DataField("stampBackgroundText")]
    public string? StampBackgroundText;

    [DataField("stampRotation")]
    public float StampRotation;
}
// DS14-end
