// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Overlays;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryHealthIconSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryNoHealthIconComponent, GetStatusIconsEvent>(OnIcons, after: [typeof(ShowHealthIconsSystem)]);
    }

    private void OnIcons(Entity<SurgeryNoHealthIconComponent> ent, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.RemoveAll(icon => icon is HealthIconPrototype);
    }
}
