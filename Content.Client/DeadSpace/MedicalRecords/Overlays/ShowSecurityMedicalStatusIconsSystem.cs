// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Overlays;
using Content.Shared.DeadSpace.MedicalRecords.Overlays;

namespace Content.Client.DeadSpace.MedicalRecords.Overlays;

/// <summary>
/// Tracks whether the local player is looking through a security HUD, so
/// <see cref="ShowMedicalStatusIconsSystem"/> can fall back to the security-visible subset. It does
/// not subscribe to <c>GetStatusIconsEvent</c> itself - the event bus allows one subscription per
/// (component, event) pair, so collecting the icons has to live in one place.
/// </summary>
public sealed class ShowSecurityMedicalStatusIconsSystem : EquipmentHudSystem<ShowSecurityMedicalStatusIconsComponent>
{
}
