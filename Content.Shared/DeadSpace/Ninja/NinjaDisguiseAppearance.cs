// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[Serializable, NetSerializable]
public sealed class NinjaDisguiseAppearance
{
    [DataField]
    public MarkingSet MarkingSet = new();

    [DataField]
    public ProtoId<SpeciesPrototype> Species = default!;

    [DataField]
    public Color SkinColor = Color.FromHex("#C0967F");

    [DataField]
    public Color EyeColor = Color.Brown;

    [DataField]
    public Color SpeakerColor = Color.White;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public Gender Gender;

    [DataField]
    public int Age = 18;

    [DataField]
    public string Voice = SharedHumanoidAppearanceSystem.DefaultVoice;

    [DataField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();

    [DataField]
    public bool HairGradientEnabled;

    [DataField]
    public Color HairGradientColor = Color.Black;
}
