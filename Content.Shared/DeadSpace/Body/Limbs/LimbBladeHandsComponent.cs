// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent]
public sealed partial class LimbBladeHandsComponent : Component
{
    public List<LimbBladeHand> Blades = new();
}

public sealed class LimbBladeHand
{
    public LimbZone Zone;
    public string Hand;
    public EntityUid Blade;

    public LimbBladeHand(LimbZone zone, string hand, EntityUid blade)
    {
        Zone = zone;
        Hand = hand;
        Blade = blade;
    }
}
