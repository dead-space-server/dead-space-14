// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Events;

namespace Content.Server.DeadSpace.ThrustersConfig;

public sealed class ThrustersConfigSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly ThrusterSystem _thrusterSystem = default!;

    private readonly HashSet<Entity<ThrusterComponent>> _thrustersSet = new();
    private const int MaxGyroscopeThrust = 2000;
    private const int MaxThrusterThrust = 100;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShuttleConsoleComponent, ThrustersRestartMessage>(OnThrustersRestartMessage);
    }

    private void OnThrustersRestartMessage(EntityUid ent, ShuttleConsoleComponent comp, ThrustersRestartMessage msg)
    {
        // Nothing the client sends is trusted: no negative, NaN or oversized thrust.
        if (!float.IsFinite(msg.ThrustersThrust) || !float.IsFinite(msg.GyroscopeThrust) ||
            msg.ThrustersThrust < 0f || msg.ThrustersThrust > MaxThrusterThrust ||
            msg.GyroscopeThrust < 0f || msg.GyroscopeThrust > MaxGyroscopeThrust)
        {
            return;
        }

        // Only the shuttle this console flies (its drone target, or its own grid) - never a grid the client names.
        if (_console.GetDroneConsole(ent) is not { } consoleUid ||
            Transform(consoleUid).GridUid is not { } shuttleEntity ||
            !HasComp<ShuttleComponent>(shuttleEntity) ||
            GetEntity(msg.ShuttleEntity) != shuttleEntity)
        {
            return;
        }

        _thrustersSet.Clear();
        _lookup.GetChildEntities(shuttleEntity, _thrustersSet);
        foreach (var thruster in _thrustersSet)
        {
            var thrusterComponent = Comp<ThrusterComponent>(thruster);
            _thrusterSystem.DisableThruster(thruster, thrusterComponent);
            switch (thrusterComponent.Type)
            {
                case ThrusterType.Angular:
                    thrusterComponent.Thrust = msg.GyroscopeThrust;
                    break;
                case ThrusterType.Linear:
                    thrusterComponent.Thrust = msg.ThrustersThrust;
                    break;
            }
            _thrusterSystem.EnableThruster(thruster, thrusterComponent);
        }
    }
}
