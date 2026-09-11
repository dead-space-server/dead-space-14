// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Atmos;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.CentComm;

public sealed class CentCommSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<Type> _allowedComponents = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CentCommStationComponent, StationPostInitEvent>(OnStationInitialized);

        foreach (var registration in EntityManager.ComponentFactory.GetAllRegistrations())
        {
            if (Attribute.IsDefined(registration.Type, typeof(AllowedGameRuleOnCentCommAttribute)))
                _allowedComponents.Add(registration.Type);
        }
    }

    private void OnStationInitialized(Entity<CentCommStationComponent> ent, ref StationPostInitEvent args)
    {
        if (_station.GetLargestGrid(ent.Owner) is not { } grid || Transform(grid).MapUid is not { } map)
            return;

        var environment = _random.Pick(_prototypes.EnumeratePrototypes<CentCommEnvironmentPrototype>().ToList());
        ApplyEnvironment(map, environment);
    }

    public void ApplyEnvironment(EntityUid map, CentCommEnvironmentPrototype environment)
    {
        var parallax = EnsureComp<ParallaxComponent>(map);
        parallax.Parallax = environment.Parallax;
        Dirty(map, parallax);
        _atmosphere.SetMapAtmosphere(map, environment.Atmosphere == null, environment.Atmosphere ?? GasMixture.SpaceGas);
    }

    public bool IsAllowedRule(EntityPrototype prototype)
    {
        if (prototype.Abstract || !prototype.HasComponent<GameRuleComponent>(EntityManager.ComponentFactory) ||
            !prototype.HasComponent<StationEventComponent>(EntityManager.ComponentFactory))
            return false;

        // These events share spawning components with otherwise supported events. Include inherited variants.
        foreach (var (ancestor, _) in _prototypes.EnumerateAllParents<EntityPrototype>(prototype.ID, includeSelf: true))
        {
            if (ancestor is "ZombieOutbreak" or "LoneOpsSpawn" or "NinjaSpawn" or "RenegadeSpawn" or "ImmovableRodSpawn" or
                "BaseWizardRule" or "BaseTraitorRule" or "ShadowlingMidround" or "BlobSpawn")
                return false;
        }

        return prototype.Components.Values.Any(entry => _allowedComponents.Contains(entry.Component.GetType()));
    }
}
