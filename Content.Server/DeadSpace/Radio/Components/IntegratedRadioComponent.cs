// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Radio.Components;

/// <summary>
/// A radio built into the creature (the tamed Ash Wolf): a handheld radio kept inside it, its frequency heard and spoken
/// on, switched on and off from the creature's own action bar. Separate from the creature, or its microphone would not
/// hear it (a microphone ignores anything with a speaker).
/// </summary>
[RegisterComponent]
public sealed partial class IntegratedRadioComponent : Component
{
    [DataField]
    public EntProtoId Radio = "RadioHandheld";

    [DataField]
    public string Container = "integrated_radio";

    [ViewVariables]
    public EntityUid? RadioEntity;
}
