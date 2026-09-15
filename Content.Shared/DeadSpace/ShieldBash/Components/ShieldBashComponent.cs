// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.ShieldBash.Components;

/// <summary>
/// Marks a shield as bashable: clicking it with an item that has
/// <see cref="ShieldBashToolComponent"/> (while either is held in hand or
/// lying in the world) plays <see cref="Sound"/>, rate-limited by
/// <see cref="Delay"/>. This is a deliberate "bang your weapon on your
/// shield" interaction (e.g. a taunt), distinct from
/// Content.Shared.Blocking.Components.BlockingComponent.BlockSound, which
/// plays when a shield actually absorbs an incoming hit while raised.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShieldBashComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier(
        "/Audio/_DeadSpace/Weapons/Melee/Shield/shieldbash.ogg",
        AudioParams.Default.WithVariation(0.15f).WithVolume(-6f));

    /// <summary>
    /// Minimum time between bash sounds on this shield, to prevent spam.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.8);
}
