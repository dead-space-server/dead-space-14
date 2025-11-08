using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Medical;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AntiAlcohol;

public sealed class AntiAlcoholSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Подписываемся на событие реакции реагента
        SubscribeLocalEvent<AntiAlcoholWatcherComponent, ReactionEntityEvent>(OnReactionEntity);
    }

    private void OnReactionEntity(Entity<AntiAlcoholWatcherComponent> ent, ref ReactionEntityEvent args)
    {
        // Проверяем что это этанол
        if (args.Reagent.ID != ent.Comp.EthanolId)
            return;

        // Проверяем метод (только Ingestion - проглатывание)
        if (args.Method != ReactionMethod.Ingestion)
            return;

        // Проверяем кулдаун
        if (_timing.CurTime < ent.Comp.NextAllowedVomitAt)
            return;

        // Проверяем наличие bloodstream
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        // Получаем химический раствор
        Entity<SolutionComponent>? solEnt = null;
        if (!_solutions.ResolveSolution((ent, null), bloodstream.ChemicalSolutionName, ref solEnt, out var solution))
            return;

        // Удаляем этанол из крови
        var ethanolAmount = args.ReagentQuantity.Quantity;
        _solutions.RemoveReagent(solEnt.Value, ent.Comp.EthanolId, ethanolAmount);

        // Шанс вызвать рвоту
        if (_random.Prob(ent.Comp.Probability))
        {
            _vomit.Vomit(ent);
            ent.Comp.NextAllowedVomitAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.CooldownSeconds);
        }
    }
}