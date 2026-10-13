// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaCloakSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceNinjaComponent, ToggleCloakNinjaEvent>(OnNinjaToggleCloak);
        SubscribeLocalEvent<NinjaCloakComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaCloakComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<NinjaCloakComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }


    private void OnNinjaToggleCloak(Entity<SpaceNinjaComponent> ent, ref ToggleCloakNinjaEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Suit is not { } suitUid)
            return;

        if (!TryComp<NinjaCloakComponent>(suitUid, out var cloak))
            return;

        cloak.Enabled = !cloak.Enabled;
        Dirty(suitUid, cloak);

        AfterToggleCloak(ent, suitUid, cloak);
    }
    protected virtual void AfterToggleCloak(Entity<SpaceNinjaComponent> ent, EntityUid suitUid, NinjaCloakComponent cloak) { }

    private void OnGetActions(Entity<NinjaCloakComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaCloakComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ActionEntity);
            ent.Comp.Enabled = false;
            Dirty(ent);
        }
    }

    private void OnUnequipped(Entity<NinjaCloakComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);
    }
}