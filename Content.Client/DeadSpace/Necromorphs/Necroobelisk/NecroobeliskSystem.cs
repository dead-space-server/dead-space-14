// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Necromorphs.Necroobelisk;

public sealed partial class NecroobeliskSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroobeliskComponent, AppearanceChangeEvent>(OnAppearanceChange);
        // DS14-start: у обелиска суперматерии визуализатора не было вовсе — спрайт оставался «unactive»,
        // даже когда машина активна и питает сеть 385 кВт. На карте, поднятой из сохранения, это
        // заметнее всего: ключей внешнему виду в файле нет, и вид раздаёт сервер на загрузке.
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, AppearanceChangeEvent>(OnSuperAppearanceChange);
        // DS14-end
    }

    // DS14-start
    private void OnSuperAppearanceChange(EntityUid uid, SuperMatterialNecroObeliskComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Ключи те же, что ставит UpdateState общей системы. Без ключа слой остаётся таким, как он
        // написан в прототипе, то есть «unactive».
        if (!_appearance.TryGetData<bool>(uid, NecroobeliskVisuals.Unactive, out var unactive, args.Component))
            return;

        args.Sprite.LayerSetState(0, unactive ? component.UnactiveState : component.State);
    }
    // DS14-end

    private void OnAppearanceChange(EntityUid uid, NecroobeliskComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, NecroobeliskVisuals.Unactive, out var unactive, args.Component))
        {
            if (unactive)
                args.Sprite.LayerSetState(0, component.UnactiveState);
            else
                args.Sprite.LayerSetState(0, component.State);
        }
    }
}
