using Content.Shared.DeadSpace.Languages.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Prison.Components;

[RegisterComponent, Access(typeof(PrisonSystem))]
public sealed partial class PrisonBoundComponent : Component
{
    public bool LanguageOverridden;
    public bool HadLanguageComponent;
    public HashSet<ProtoId<LanguagePrototype>> PreviousKnownLanguages = [];
    public HashSet<ProtoId<LanguagePrototype>> PreviousCantSpeakLanguages = [];
    public HashSet<ProtoId<LanguagePrototype>> PreviousUnlockLanguages = [];
    public string PreviousSelectedLanguage = string.Empty;
}
