// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Weapons.Parry;

public sealed partial class ParryCooldownOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ParryCooldownOverlay(EntityManager, _timing, _input, _players, _hands));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<ParryCooldownOverlay>();
    }
}
