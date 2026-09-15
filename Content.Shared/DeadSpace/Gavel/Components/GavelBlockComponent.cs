// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Gavel.Components;

/// <summary>
/// Marks an entity as a judge's gavel block/sound block. Being struck by an
/// item with <see cref="GavelHammerComponent"/> plays <see cref="Sound"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GavelBlockComponent : Component
{
    /// <summary>
    /// Sound played when struck with a gavel hammer.
    /// <see cref="AudioParams.Variation"/> randomizes the pitch a little on
    /// every hit (a Gaussian scramble around the base pitch), so the same
    /// single .ogg file never sounds exactly the same twice — the SS14
    /// equivalent of the old "vary" flag on BYOND's playsound().
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier(
        "/Audio/_DeadSpace/Items/Gavel/gavel.ogg",
        AudioParams.Default.WithVariation(0.125f));
}
