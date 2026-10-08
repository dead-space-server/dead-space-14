// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Medical.Components;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Inventory.Events;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class SelfHealthAnalyzerSystem : SharedSelfHealthAnalyzerSystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SelfHealthAnalyzerComponent, SelfAnalyzeActionEvent>(OnSelfAnalyzeAction);
        SubscribeLocalEvent<SelfHealthAnalyzerComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnSelfAnalyzeAction(Entity<SelfHealthAnalyzerComponent> ent, ref SelfAnalyzeActionEvent args)
    {
        args.Handled = true;

        if (!HasComp<MobStateComponent>(args.Performer)) 
            return;
        if (!TryComp<HealthAnalyzerComponent>(ent.Owner, out var healthAnalyzer)) 
            return;
        if (!_uiSystem.HasUi(ent.Owner, HealthAnalyzerUiKey.Key)) 
            return;

        if (!healthAnalyzer.Silent)
            _audio.PlayPvs(healthAnalyzer.ScanningEndSound, ent.Owner);

        healthAnalyzer.ScannedEntity = args.Performer;
        _uiSystem.OpenUi(ent.Owner, HealthAnalyzerUiKey.Key, args.Performer);
    }

    private void OnGotUnequipped(Entity<SelfHealthAnalyzerComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<HealthAnalyzerComponent>(ent.Owner, out var healthAnalyzer)) 
            return;

        healthAnalyzer.ScannedEntity = null;
        if (_uiSystem.IsUiOpen(ent.Owner, HealthAnalyzerUiKey.Key, args.Equipee))
            _uiSystem.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key, args.Equipee);
    }
}