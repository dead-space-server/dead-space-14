// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Content.Shared.DeadSpace.Necromorphs.NecroobeliskStoper;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;

namespace Content.Server.DeadSpace.Necromorphs.NecroobeliskStoper;

public sealed partial class NecroobeliskStoperSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private NecroobeliskSystem _necroobeliskSystem = default!;
    [Dependency] private SuperNecroobeliskSystem _superNecroobeliskSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroobeliskStoperComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<NecroobeliskStoperComponent, NecroobeliskStoperDoAfterEvent>(OnDoAfter);
    }

    private void OnScannerAfterInteract(EntityUid uid, NecroobeliskStoperComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (TryComp<NecroobeliskComponent>(args.Target, out var necroobeliskComponent))
        {
            if (!necroobeliskComponent.IsStoper)
                return;

            if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new NecroobeliskStoperDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                DistanceThreshold = 2f
            }))
                args.Handled = true;

            return;
        }
        if (TryComp<SuperMatterialNecroObeliskComponent>(args.Target, out var superMatterialNecroObeliskComponent))
        {
            if (!superMatterialNecroObeliskComponent.IsStoper)
                return;

            if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new NecroobeliskStoperDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                DistanceThreshold = 2f
            }))
                args.Handled = true;

            return;
        }
        if (HasComp<NecroobeliskSplinterComponent>(args.Target))
        {
            if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ScanDoAfterDuration, new NecroobeliskStoperDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnDamage = true,
                DistanceThreshold = 2f
            }))
                args.Handled = true;
        }

    }

    private void OnDoAfter(EntityUid uid, NecroobeliskStoperComponent component, NecroobeliskStoperDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;

        if (TryComp<NecroobeliskComponent>(target, out var necroobeliskComp))
        {
            _necroobeliskSystem.ToggleObeliskActive(target, necroobeliskComp);
            _audio.PlayPvs(component.CompleteSound, uid);
            QueueDel(uid);
            args.Handled = true;
            return;
        }
        if (TryComp<SuperMatterialNecroObeliskComponent>(target, out var comp))
        {
            if (!comp.SequenceStarted && comp.Percents != 99)
                _superNecroobeliskSystem.StartActivationSequence(target, comp);
            else if (comp.Percents >= 5) comp.Percents -= 5;
        }

        if (TryComp<NecroobeliskSplinterComponent>(target, out var splinter))
        {
            var ev = new NecroSplinterAfterStoperEvent();
            RaiseLocalEvent(target, ref ev);
        }

        QueueDel(uid);

        args.Handled = true;
    }
}
