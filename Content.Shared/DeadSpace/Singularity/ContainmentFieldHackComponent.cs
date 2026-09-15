// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Singularity;

[RegisterComponent]
public sealed partial class ContainmentFieldHackComponent : Component
{
    /// <summary>
    /// Time in seconds before the hacked generator fatally destabilizes.
    /// </summary>
    [DataField]
    public float DestabilizationDuration = 60f;
}
