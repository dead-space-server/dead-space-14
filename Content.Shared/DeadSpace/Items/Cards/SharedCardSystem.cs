
using Content.Shared.DeadSpace.Items.Cards.Components;

namespace Content.Shared.DeadSpace.Items.Cards;

public abstract partial class SharedCardSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public void TurnOver(EntityUid uid, CardComponent component)
    {
        if (component.IsReserve)
        {
            component.IsReserve = false;
            _appearance.SetData(uid, CardVisuals.Reserve, false);
        }
        else
        {
            component.IsReserve = true;
            _appearance.SetData(uid, CardVisuals.Reserve, true);
        }
    }

}
