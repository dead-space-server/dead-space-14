// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.DeadSpace.Languages.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class LanguageComponent : Component
{
    [DataField]
    public List<string> KnownLanguages = new();

    [DataField]
    public string SelectedLanguage = String.Empty;

    [DataField("selectLanguageAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? SelectLanguageAction = "SelectLanguageAction";

    [DataField]
    public EntityUid? SelectLanguageActionEntity;
}


