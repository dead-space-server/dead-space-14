// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.Virus;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Server.Popups;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Virus.Systems;

public sealed partial class PrimaryPacientSystem : EntitySystem
{
    [Dependency] private SentientVirusSystem _sentientVirusSystem = default!;
    [Dependency] private VirusSystem _virus = default!;
    [Dependency] private TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    private const int Compensation = 5000;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrimaryPacientComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PrimaryPacientComponent, CureVirusEvent>(OnCureVirus);
        SubscribeLocalEvent<PrimaryPacientComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<PrimaryPacientComponent, EnterCryostorageEvent>(OnMindRemoved);
    }

    private void OnMindRemoved(EntityUid uid, PrimaryPacientComponent component, EnterCryostorageEvent args)
    {
        if (!TryComp<SentientVirusComponent>(component.SentientVirus, out var sentientVirusComp))
            return;

        if (sentientVirusComp.Data != null)
        {
            sentientVirusComp.Data.MutationPoints += Compensation;
            sentientVirusComp.FactPrimaryInfected--;
            _popupSystem.PopupEntity(
                Loc.GetString("sentient-virus-infect-compensation", ("price", Compensation)),
                component.SentientVirus.Value,
                component.SentientVirus.Value,
                PopupType.Medium
            );
        }

        _virus.CureVirus(uid);
    }

    private void OnInit(Entity<PrimaryPacientComponent> entity, ref ComponentInit args)
    {
        _timedWindowSystem.Reset(entity.Comp.UpdateWindow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PrimaryPacientComponent, VirusComponent>();
        while (query.MoveNext(out var uid, out var component, out var virusComponent))
        {
            if (_timedWindowSystem.IsExpired(component.UpdateWindow))
            {
                _timedWindowSystem.Reset(component.UpdateWindow);
                _virus.InfectAround((uid, virusComponent), component.RangeInfect);
            }
        }
    }

    private void OnCureVirus(EntityUid uid, PrimaryPacientComponent component, CureVirusEvent args)
    {
        RemComp<PrimaryPacientComponent>(uid);
    }

    private void OnRemove(EntityUid uid, PrimaryPacientComponent component, ComponentRemove args)
    {
        if (component.SentientVirus != null
            && TryComp<SentientVirusComponent>(component.SentientVirus, out var sentientVirus))
            _sentientVirusSystem.RemovePrimaryInfected(component.SentientVirus.Value, uid, sentientVirus);
    }

}
