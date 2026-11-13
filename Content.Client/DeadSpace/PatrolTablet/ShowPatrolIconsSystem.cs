using Content.Client.Overlays;
using Content.Shared.Access.Systems;
using Content.Shared.DeadSpace.PatrolTablet;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.PatrolTablet;

public sealed partial class ShowPatrolIconsSystem : EquipmentHudSystem<ShowPatrolIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PatrolMemberComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, PatrolMemberComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (!_idCard.TryFindIdCard(uid, out var idCard))
            return;

        if (!TryComp<PatrolSquadCardComponent>(idCard.Owner, out var squadCard))
            return;

        if (!string.IsNullOrEmpty(squadCard.SquadIcon) &&
            _prototype.TryIndex<SecurityIconPrototype>(squadCard.SquadIcon, out var iconProto))
        {
            ev.StatusIcons.Add(iconProto);
        }
    }
}
