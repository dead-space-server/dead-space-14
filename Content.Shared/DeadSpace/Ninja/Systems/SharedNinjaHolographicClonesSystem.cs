// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaHolographicClonesSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHolographicClonesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaHolographicClonesComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<NinjaHolographicClonesComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }

    private void OnMapInit(Entity<NinjaHolographicClonesComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(uid, ref comp.HolographicClonesActionEntity, comp.HolographicClonesAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaHolographicClonesComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.HolographicClonesActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaHolographicClonesComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.HolographicClonesActionEntity);
        }
    }
}