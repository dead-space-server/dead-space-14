// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Virus.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Virus.Systems;

public sealed class VirusRule : StationEventSystem<VirusRuleComponent>
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly VirusSystem _virusSystem = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("VirusRule");
    }

    protected override void Added(EntityUid uid, VirusRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        List<EntityUid> ents = new List<EntityUid>();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is { } entity && RuleStation.IsTarget(uid, entity))
                ents.Add(entity);
        }

        var strainId = _virusSystem.GenerateStrainId();
        if (component.Data == null)
            component.Data = _virusSystem.GenerateVirusData(strainId, component.SymptomsByDanger, component.BodyCount);

        var validEntities = ents
            .Where(ent => _virusSystem.CanInfect(ent, component.Data))
            .ToList();

        if (validEntities.Count <= 0)
        {
            _sawmill.Info("Не найдено сущностей, которых можно подвергнуть заражению вирусом.");
            return;
        }

        var toAdd = Math.Min(component.NumberPrimaryPacienst, validEntities.Count);

        for (var i = 0; i < toAdd; i++)
        {
            var picked = _random.PickAndTake(validEntities);

            _virusSystem.InfectEntity(component.Data, picked);

            var comp = EnsureComp<PrimaryPacientComponent>(picked);
            comp.StrainId = strainId;

            if (validEntities.Count == 0)
                break;
        }

    }
}
