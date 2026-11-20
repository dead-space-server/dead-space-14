// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Surgery;

public sealed partial class SurgeryAppendageVisualSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, List<string>> _drawn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryAppendageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurgeryAppendageComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SurgeryAppendageComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SurgeryAppendageComponent> ent, ref ComponentStartup args)
    {
        Paint(ent);
    }

    private void OnState(Entity<SurgeryAppendageComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Paint(ent);
    }

    private void OnShutdown(Entity<SurgeryAppendageComponent> ent, ref ComponentShutdown args)
    {
        _drawn.Remove(ent.Owner);
    }

    private void Paint(Entity<SurgeryAppendageComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_drawn.TryGetValue(ent.Owner, out var old))
        {
            var indexes = new List<int>();
            foreach (var key in old)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                {
                    _sprite.LayerMapRemove((ent.Owner, sprite), key);
                    indexes.Add(index);
                }
            }

            indexes.Sort();
            for (var i = indexes.Count - 1; i >= 0; i--)
                _sprite.RemoveLayer((ent.Owner, sprite), indexes[i]);
            old.Clear();
        }
        else
        {
            old = new List<string>();
            _drawn[ent.Owner] = old;
        }

        foreach (var marking in ent.Comp.Markings)
        {
            if (!_proto.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto))
                continue;

            for (var j = 0; j < proto.Sprites.Count; j++)
            {
                if (proto.Sprites[j] is not SpriteSpecifier.Rsi rsi)
                    continue;

                var key = $"appendage-{marking.MarkingId}-{j}";
                var layer = _sprite.AddLayer((ent.Owner, sprite), rsi);
                _sprite.LayerMapSet((ent.Owner, sprite), key, layer);
                _sprite.LayerSetColor((ent.Owner, sprite), key, j < marking.MarkingColors.Count ? marking.MarkingColors[j] : Color.White);
                old.Add(key);
            }
        }
    }
}
