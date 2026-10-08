// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Actions;
using Content.Shared.Ninja.Components;
using Content.Server.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaCloakSystem : SharedNinjaCloakSystem
{
    [Dependency] private readonly NinjaSmokeAbilitySystem _ninjaSmoke = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaCloakComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<NinjaCloakComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    protected override void AfterToggleCloak(Entity<SpaceNinjaComponent> ent, EntityUid suitUid, NinjaCloakComponent cloak)
    {
        base.AfterToggleCloak(ent, suitUid, cloak);

        if (cloak.Enabled)
        {
            if (TryComp<NinjaSmokeAbilityComponent>(suitUid, out var smokeComp) && smokeComp.AutoMode)
            {
                _ninjaSmoke.TrySpawnNinjaSmoke((suitUid, smokeComp), autoMode: true);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<NinjaCloakComponent>();
        while (query.MoveNext(out var uid, out var cloak))
        {
            if (!cloak.Enabled)
                continue;

            var user = Transform(uid).ParentUid;
            if (!user.IsValid())
                continue;

            float cost = cloak.DrainRate * frameTime;

            if (!_ninja.TryUseCharge(user, cost))
            {
                cloak.Enabled = false;
                Dirty(uid, cloak);
            }
        }
    }
}