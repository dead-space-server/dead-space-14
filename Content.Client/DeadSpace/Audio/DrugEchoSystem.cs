// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Narcotics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client.DeadSpace.Audio;

/// <summary>
/// Tells the room-echo system to apply the strong "Drugged" reverb preset while the
/// local player has any narcotic reagent in their bloodstream.
/// </summary>
public sealed class DrugEchoSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly AreaEchoSystem _areaEcho = default!;

    private bool _wasActive;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var local = _player.LocalEntity;
        var active = local is { } uid && HasComp<DrugEchoComponent>(uid);
        if (active == _wasActive)
            return;

        _wasActive = active;
        _areaEcho.DrugEchoOverride = active;
    }
}