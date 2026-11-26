// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.UserInterface.Hologram.Sound;

/// <summary>
/// Button click and hover sounds that replace the server's ones, with their own gain before the interface volume.
/// </summary>
/// <seealso cref="Content.Client.Audio.AudioUIController.SetSoundOverride"/>
public sealed record UISoundOverride(string Click, float ClickGain, string Hover, float HoverGain);
