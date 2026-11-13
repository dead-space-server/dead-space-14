using System.Linq;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Content.Shared.Research.Prototypes;
using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;
using Content.Server.DeadSpace.Virus.Systems;
using Content.Server.DeadSpace.Virus.Components;

namespace Content.Server.Research.Disk
{
    public sealed partial class ResearchDiskSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototype = default!;
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private ResearchSystem _research = default!;
        [Dependency] private VirusDiagnoserDataServerSystem _diagnoserDataServer = default!; // DS14
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ResearchDiskComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<ResearchDiskComponent, MapInitEvent>(OnMapInit);
        }

        private void OnAfterInteract(EntityUid uid, ResearchDiskComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            // DS14-start
            if (TryComp<ResearchServerComponent>(args.Target, out var server))
            {
                _research.ModifyServerPoints(args.Target.Value, component.Points, server);
            }
            else if (TryComp<VirusDiagnoserDataServerComponent>(args.Target, out var virusDiagnoserDataServer))
            {
                _diagnoserDataServer.AddPoints((args.Target.Value, virusDiagnoserDataServer), component.Points);
            }
            else
            {
                return;
            }
            // DS14-end

            _popupSystem.PopupEntity(Loc.GetString("research-disk-inserted", ("points", component.Points)), args.Target.Value, args.User);
            QueueDel(uid);
            args.Handled = true;
        }

        private void OnMapInit(EntityUid uid, ResearchDiskComponent component, MapInitEvent args)
        {
            if (!component.UnlockAllTech)
                return;

            component.Points = _prototype.EnumeratePrototypes<TechnologyPrototype>()
                .Sum(tech => tech.Cost);
        }
    }
}
