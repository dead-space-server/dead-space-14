// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Surgery;

public sealed partial class SurgeryMmiBellySystem : EntitySystem
{
    private const string Shell = "surgery-mmi-belly";
    private const string Brain = "surgery-mmi-belly-brain";
    private static readonly ResPath Rsi = new("/Textures/_DeadSpace/Objects/Specific/Robotics/mmi.rsi");
    private static readonly Vector2 Offset = new(0f, -0.12f);
    private static readonly Vector2 Scale = new(0.45f, 0.45f);

    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryMmiBellyComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<SurgeryMmiBellyComponent, SpriteComponent, SurgeryCavityComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var cavity))
            Paint(uid, sprite, cavity);
    }

    private void OnShutdown(Entity<SurgeryMmiBellyComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        Entity<SpriteComponent?> body = (ent.Owner, sprite);
        if (_sprites.LayerMapTryGet(body, Shell, out _, false))
            _sprites.LayerSetVisible(body, Shell, false);
        if (_sprites.LayerMapTryGet(body, Brain, out _, false))
            _sprites.LayerSetVisible(body, Brain, false);
    }

    private void Paint(EntityUid uid, SpriteComponent sprite, SurgeryCavityComponent cavity)
    {
        Entity<SpriteComponent?> ent = (uid, sprite);
        Reserve(ent, Shell, "mmi_off");
        Reserve(ent, Brain, "mmi_brain");

        var south = _xform.GetWorldRotation(uid).GetCardinalDir() == Direction.South;
        var bare = !LimbCoverRules.Worn(uid, LimbZone.Groin, EntityManager, _inventory);
        EntityUid? mmi = TryGetEntity(cavity.AbdomenItem, out var lodged) && HasComp<MMIComponent>(lodged) ? lodged : null;
        var shown = south && bare && mmi != null;
        _sprites.LayerSetVisible(ent, Shell, shown);
        _sprites.LayerSetVisible(ent, Brain, false);
        if (!shown || !TryComp<SpriteComponent>(mmi, out var jar))
            return;

        Entity<SpriteComponent?> source = (mmi!.Value, jar);
        if (Copy(source, MMIVisualLayers.Base, out var shellState, out _))
            _sprites.LayerSetRsiState(ent, Shell, shellState);

        if (Copy(source, MMIVisualLayers.Brain, out var brainState, out var brainShown) && brainShown)
        {
            _sprites.LayerSetRsiState(ent, Brain, brainState);
            _sprites.LayerSetVisible(ent, Brain, true);
        }
    }

    private void Reserve(Entity<SpriteComponent?> ent, string key, string state)
    {
        if (_sprites.LayerMapTryGet(ent, key, out _, false))
            return;

        _sprites.LayerMapReserve(ent, key);
        _sprites.LayerSetRsi(ent, key, Rsi, state);
        _sprites.LayerSetOffset(ent, key, Offset);
        _sprites.LayerSetScale(ent, key, Scale);
    }

    private bool Copy(Entity<SpriteComponent?> jar, MMIVisualLayers key, out string state, out bool visible)
    {
        state = "";
        visible = false;
        if (!_sprites.LayerMapTryGet(jar, key, out var index, false)
            || jar.Comp?[index] is not SpriteComponent.Layer layer)
            return false;

        state = layer.State.Name ?? "";
        visible = layer.Visible;
        return state.Length > 0;
    }
}
