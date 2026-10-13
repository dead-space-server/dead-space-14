// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaSpiritFormSystem : SharedNinjaSpiritFormSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, EntityUid> _tinted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSpiritFormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NinjaSpiritFormComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<NinjaSpiritFormComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<NinjaSpiritFormComponent>();

        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.SpiritFormActive)
                continue;

            ApplyTint(suitUid, comp);
        }
    }

    protected override void SetSpiritAppearance(
        Entity<NinjaSpiritFormComponent> ent,
        bool phasing)
    {
        if (phasing)
            ApplyTint(ent.Owner, ent.Comp);
        else
            Restore(ent.Owner);
    }

    private void OnStartup(Entity<NinjaSpiritFormComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.SpiritFormActive)
            ApplyTint(ent.Owner, ent.Comp);
    }

    private void OnHandleState(Entity<NinjaSpiritFormComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (args.State is not NinjaSpiritFormComponent.NinjaSpiritFormComponent_AutoState state)
            return;

        if (state.SpiritFormActive)
            ApplyTint(ent.Owner, ent.Comp);
        else
            Restore(ent.Owner);
    }

    private void OnShutdown(Entity<NinjaSpiritFormComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent.Owner);
    }

    private void ApplyTint(EntityUid suitUid, NinjaSpiritFormComponent comp)
    {
        var wearer = Transform(suitUid).ParentUid;

        if (!wearer.IsValid() || !TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        if (_tinted.TryGetValue(suitUid, out var previous) && previous != wearer)
            Restore(suitUid);

        _sprite.SetColor((wearer, sprite), comp.SpiritFormColor);
        _tinted[suitUid] = wearer;
    }

    private void Restore(EntityUid suitUid)
    {
        if (!_tinted.Remove(suitUid, out var wearer))
            return;

        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        _sprite.SetColor((wearer, sprite), Color.White);
    }
}
