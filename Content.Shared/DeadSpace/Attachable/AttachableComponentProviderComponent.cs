// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Attachable;

/// <summary>
/// Copies selected components from an installed attachment to its holder.
/// Component names use their YAML registration names.
/// </summary>
[RegisterComponent]
public sealed partial class AttachableComponentProviderComponent : Component
{
    [DataField(required: true)]
    public HashSet<string> Components = new();
}
