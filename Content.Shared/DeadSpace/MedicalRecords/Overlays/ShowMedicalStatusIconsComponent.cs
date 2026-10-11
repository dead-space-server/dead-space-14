// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.MedicalRecords.Overlays;

/// <summary>
/// Marker granting HUD visibility of the full medical status set. Worn by anything parenting
/// <c>ShowMedicalIcons</c>, so a medical HUD activates it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowMedicalStatusIconsComponent : Component
{
}
