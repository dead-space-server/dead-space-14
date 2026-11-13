using System.Linq;
using Content.Server.Silicons.Laws;
using Content.Shared.DeadSpace.LawConfigurator.Systems;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server.DeadSpace.LawConfigurator;

public sealed partial class LawConfiguratorServerSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _siliconLaw = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConfigureLawsFromBoardEvent>(OnConfigureLawsFromBoard);
    }

    private void OnConfigureLawsFromBoard(ConfigureLawsFromBoardEvent args)
    {
        if (!TryComp<SiliconLawProviderComponent>(args.Board, out var boardLawProvider))
            return;

        var ev = new GetSiliconLawsEvent(args.Board);
        RaiseLocalEvent(args.Board, ref ev);
        if (!ev.Handled)
            return;

        var laws = ev.Laws.Laws.Select(x => x.ShallowClone()).ToList();
        _siliconLaw.SetLaws(laws, args.Target, boardLawProvider.LawUploadSound);

        // Флаг Subverted останется прежним
    }
}
