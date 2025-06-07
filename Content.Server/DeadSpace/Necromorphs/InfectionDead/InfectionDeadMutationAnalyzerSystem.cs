// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Interaction;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.Paper;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Necromorphs.InfectionDead;

public sealed class InfectionDeadMutationAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfectionDeadMutationAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    public override void Update(float frameTime)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<InfectionDeadMutationAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (curTime >= component.RunningTime && component.IsRunning)
                Running(uid, component);
        }
    }

    private void OnAfterInteract(EntityUid uid, InfectionDeadMutationAnalyzerComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled || component.IsRunning)
            return;

        if (!TryComp<NecromorfComponent>(args.Target, out var necro))
            return;

        component.IsRunning = true;
        component.Target = args.Target;
        component.RunningTime = _gameTiming.CurTime + component.DurationRunning;
        _audio.PlayPvs(component.PrintingSound, uid);
    }

    public void Running(EntityUid uid, InfectionDeadMutationAnalyzerComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Target == null)
            return;

        if (!TryComp<NecromorfComponent>(component.Target, out var necro))
        {
            component.IsRunning = false;
            return;
        }

        var strainData = necro.StrainData;

        // Собираем свойства для вывода
        string output = "Параметры InfectionDeadStrainData:\n";

        output += $"Модификатор урона: {strainData.DamageMulty}\n";
        output += $"Модификатор выносливости: {strainData.StaminaMulty}\n";
        output += $"Модификатор здоровья: {strainData.HpMulty}\n";
        output += $"Модификатор скорости: {strainData.SpeedMulty}\n";

        output += $"\nОсобые мутации: \n";

        output += necro.StrainData.Effects.ToString();

        var paper = Spawn(component.Paper, Transform(uid).Coordinates);
        if (!TryComp<PaperComponent>(paper, out var paperComp))
        {
            QueueDel(paper);
            return;
        }

        var content = Loc.GetString(output);

        _paperSystem.SetContent((paper, paperComp), content);

        component.IsRunning = false;
    }
}

