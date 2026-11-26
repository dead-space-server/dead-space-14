// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// Draws the cardinal's screams: the rings rolling out, and for hunting fighters bodies through walls and arrows to
/// marked outsiders off the screen.
/// </summary>
public sealed partial class CardinalScreamSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private CardinalScreamWaveOverlay _waves = default!;
    private CardinalHuntOverlay _hunt = default!;

    public override void Initialize()
    {
        base.Initialize();

        _waves = new CardinalScreamWaveOverlay(EntityManager, _timing, _transform);
        _hunt = new CardinalHuntOverlay(EntityManager, _player, _timing, _lookup, _sprite, _transform);
        _overlays.AddOverlay(_waves);
        _overlays.AddOverlay(_hunt);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_waves);
        _overlays.RemoveOverlay(_hunt);
    }
}
