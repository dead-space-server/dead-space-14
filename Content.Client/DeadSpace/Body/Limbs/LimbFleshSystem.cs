// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbFleshSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, List<string>> _drawn = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbFleshComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimbFleshComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LimbFleshComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<LimbFleshComponent> ent, ref ComponentStartup args)
    {
        Paint(ent);
    }

    private void OnShutdown(Entity<LimbFleshComponent> ent, ref ComponentShutdown args)
    {
        _drawn.Remove(ent.Owner);
    }

    private void OnState(Entity<LimbFleshComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Paint(ent);
    }

    private void Paint(Entity<LimbFleshComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetColor((ent.Owner, sprite), Color.White);
        if (sprite.TryGetLayer(0, out _))
            _sprite.LayerSetColor((ent.Owner, sprite), 0, LimbSpeciesRules.Look(ent.Comp.Bare, ent.Comp.Skin));

        Clear(ent.Owner, sprite);
        if (ent.Comp.Bare)
            return;

        if (!_drawn.TryGetValue(ent.Owner, out var ids))
        {
            ids = new List<string>();
            _drawn[ent.Owner] = ids;
        }

        foreach (var marking in ent.Comp.Marks)
        {
            if (!_proto.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto))
                continue;

            for (var j = 0; j < proto.Sprites.Count; j++)
            {
                if (proto.Sprites[j] is not SpriteSpecifier.Rsi rsi)
                    continue;

                var id = $"limb-{marking.MarkingId}-{rsi.RsiState}";
                var layer = _sprite.AddLayer((ent.Owner, sprite), rsi);
                _sprite.LayerMapSet((ent.Owner, sprite), id, layer);
                var color = j < marking.MarkingColors.Count ? marking.MarkingColors[j] : Color.White;
                _sprite.LayerSetColor((ent.Owner, sprite), id, color);
                _sprite.LayerSetScale((ent.Owner, sprite), id, proto.SpriteLayers[j].Scale);
                ids.Add(id);
            }
        }

        if (ent.Comp.EyeSprite.Length == 0)
            return;

        var eyes = new SpriteSpecifier.Rsi(new ResPath(ent.Comp.EyeSprite), ent.Comp.EyeState);
        var eyeLayer = _sprite.AddLayer((ent.Owner, sprite), eyes);
        _sprite.LayerMapSet((ent.Owner, sprite), "limb-eyes", eyeLayer);
        _sprite.LayerSetColor((ent.Owner, sprite), "limb-eyes", ent.Comp.Eyes);
        ids.Add("limb-eyes");
    }

    private void Clear(EntityUid uid, SpriteComponent sprite)
    {
        if (!_drawn.TryGetValue(uid, out var ids))
            return;

        var indexes = new List<int>();
        foreach (var id in ids)
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), id, out var index, false))
                continue;

            _sprite.LayerMapRemove((uid, sprite), id);
            indexes.Add(index);
        }

        indexes.Sort();
        for (var i = indexes.Count - 1; i >= 0; i--)
            _sprite.RemoveLayer((uid, sprite), indexes[i]);

        ids.Clear();
    }
}
