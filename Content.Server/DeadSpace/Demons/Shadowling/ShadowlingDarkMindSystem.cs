// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Mobs.Systems;
using Content.Shared.DeadSpace.Demons.Shadowling;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingDarkMindSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowlingDarkMindComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShadowlingDarkMindComponent, ShadowlingDarkMindEvent>(OnDarkMind);
    }

    private void OnComponentInit(EntityUid uid, ShadowlingDarkMindComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionDarkMindEntity, component.ActionDarkMind);
    }

    private void OnDarkMind(EntityUid uid, ShadowlingDarkMindComponent component, ShadowlingDarkMindEvent args)
    {
        if (args.Handled) return;

        int personalSlaves = 0;
        var query = EntityQueryEnumerator<ShadowlingSlaveComponent>();

        while (query.MoveNext(out var sUid, out var slave))
        {
            if (slave.Master == uid && _mobState.IsAlive(sUid))
            {
                personalSlaves++;
            }
        }

        _popup.PopupEntity($"У вас {personalSlaves} живых порабощённых.", uid, uid, PopupType.Medium);

        args.Handled = true;
    }
}