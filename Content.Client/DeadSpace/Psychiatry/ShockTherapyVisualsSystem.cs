// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Psychiatry;
using Robust.Shared.GameObjects;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Psychiatry;

public sealed class ShockTherapyVisualsSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly PowerCellSystem _cells = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private readonly Dictionary<EntityUid, string> _shown = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShockTherapyComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ShockTherapyComponent> ent, ref ComponentShutdown args)
    {
        _shown.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShockTherapyComponent, SpriteComponent, ItemToggleComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var toggle))
        {
            string state;
            if (_appearance.TryGetData(uid, ShockTherapyVisuals.Shocking, out bool shocking) && shocking)
            {
                state = "shock";
            }
            else
            {
                var step = 0;
                if (_cells.TryGetBatteryFromSlot(uid, out var battery))
                    step = ContentHelpers.RoundToNearestLevels(_battery.GetChargeLevel(battery.Value.AsNullable()), 1, 3);

                state = $"{(_toggle.IsActivated((uid, toggle)) ? "on" : "off")}-{step}";
            }
            if (_shown.TryGetValue(uid, out var shown) && shown == state)
                continue;

            _sprite.LayerSetRsiState((uid, sprite), 0, state);
            _shown[uid] = state;
        }
    }
}
