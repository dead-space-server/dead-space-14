// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Alerts;

namespace Content.Server.Guardian;

public sealed partial class GuardianSystem
{
    [Dependency] private HealthAlertRelaySystem _healthAlertRelay = default!;

    partial void OnGuardianCreated(EntityUid guardian, EntityUid host)
    {
        SetHealthAlertRelay(guardian, host);
    }

    private void OnGuardianStartup(EntityUid uid, GuardianComponent component, ComponentStartup args)
    {
        SetHealthAlertRelay(uid, component.Host);
    }

    private void SetHealthAlertRelay(EntityUid guardian, EntityUid? host)
    {
        if (!TryComp<HealthAlertRelayComponent>(guardian, out var relay))
            return;

        relay.LinkedEntity = host;
        Dirty(guardian, relay);
        _healthAlertRelay.UpdateRelay(guardian, relay);
    }
}
