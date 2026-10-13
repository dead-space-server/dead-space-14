// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.MedicalRecords.Overlays;

/// <summary>
/// Marker granting HUD visibility of the medical statuses security acts on - "psychically unstable"
/// and "needs forced treatment". Worn by anything parenting <c>ShowSecurityIcons</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowSecurityMedicalStatusIconsComponent : Component
{
}
