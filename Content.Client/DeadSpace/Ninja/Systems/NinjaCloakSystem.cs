// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.ThermalVision;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaCloakSystem : SharedNinjaCloakSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, EntityUid> _hidden = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaCloakComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NinjaCloakComponent, AfterAutoHandleStateEvent>(OnStateChanged);
        SubscribeLocalEvent<NinjaCloakComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<NinjaCloakComponent>();

        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            ApplyCloaked(suitUid, comp);
        }
    }

    private void OnStartup(Entity<NinjaCloakComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Enabled)
            ApplyCloaked(ent.Owner, ent.Comp);
    }

    private void OnStateChanged(Entity<NinjaCloakComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (args.State is not NinjaCloakComponent.NinjaCloakComponent_AutoState)
            return;

        if (ent.Comp.Enabled)
            ApplyCloaked(ent.Owner, ent.Comp);
        else
            Restore(ent.Owner);
    }

    private void OnShutdown(Entity<NinjaCloakComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent.Owner);
    }

    private void ApplyCloaked(EntityUid suitUid, NinjaCloakComponent comp)
    {
        var wearer = Transform(suitUid).ParentUid;

        if (!wearer.IsValid() || !TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        if (_hidden.TryGetValue(suitUid, out var previous) && previous != wearer)
            Restore(suitUid);

        _sprite.SetVisible((wearer, sprite), false);
        _hidden[suitUid] = wearer;

        if (TryComp<ThermalVisibleComponent>(wearer, out var thermal))
            thermal.DrawWhenInvisible = true;
    }

    private void Restore(EntityUid suitUid)
    {
        if (!_hidden.Remove(suitUid, out var wearer))
            return;

        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        _sprite.SetVisible((wearer, sprite), true);

        if (TryComp<ThermalVisibleComponent>(wearer, out var thermal))
            thermal.DrawWhenInvisible = false;
    }
}
