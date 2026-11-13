// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Physics.Systems;
using Content.Shared.Gibbing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingAnnihilationSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingAnnihilationComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShadowlingAnnihilationComponent, ShadowlingAnnihilationEvent>(OnAnnihilationAction);
    }

    private void OnComponentInit(EntityUid uid, ShadowlingAnnihilationComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionAnnihilationEntity, component.ActionAnnihilation);
    }

    private void OnAnnihilationAction(EntityUid uid, ShadowlingAnnihilationComponent component, ShadowlingAnnihilationEvent args)
    {
        if (args.Handled) return;

        var target = args.Target;
        var performer = args.Performer;
        var meta = MetaData(target);

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return;

        if (target == performer || HasComp<ShadowlingComponent>(target))
            return;

        if (meta.EntityPrototype?.ID == component.ImmunePrototypeId)
            return;

        var targetPos = _transform.GetMapCoordinates(target).Position;
        var performerPos = _transform.GetMapCoordinates(performer).Position;
        var direction = targetPos - performerPos;

        if (direction.LengthSquared() > 0)
        {
            var impulseVector = direction.Normalized() * 10000f;
            _physics.ApplyLinearImpulse(target, impulseVector);
        }

        _audio.PlayPvs(new SoundCollectionSpecifier("ShadowlingAnnihilation"), uid);

        if (TryComp<BodyComponent>(target, out var body))
            _gibbing.Gib(target);

        if (Exists(target) && !TerminatingOrDeleted(target))
            QueueDel(target);

        args.Handled = true;
    }
}