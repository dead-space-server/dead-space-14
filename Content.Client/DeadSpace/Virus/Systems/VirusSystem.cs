// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Virus.Components;
using Robust.Client.Player;
using Content.Shared.DeadSpace.Virus;

namespace Content.Client.DeadSpace.Virus.Systems;

public sealed partial class VirusSystem : SharedVirusSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirusComponent, GetStatusIconsEvent>(GetPacient);
        SubscribeLocalEvent<PrimaryPacientComponent, GetStatusIconsEvent>(GetPrimaryPacient);
    }

    private void GetPacient(Entity<VirusComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_player.LocalEntity == ent)
            return;

        if (HasComp<PrimaryPacientComponent>(ent))
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }

    private void GetPrimaryPacient(Entity<PrimaryPacientComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_player.LocalEntity == ent)
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }

}
