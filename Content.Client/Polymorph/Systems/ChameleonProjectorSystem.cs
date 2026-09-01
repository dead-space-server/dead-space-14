using Content.Client.Effects;
using Content.Client.Humanoid;
using Content.Client.Smoking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Humanoid;
using Content.Shared.Polymorph.Components;
using Content.Shared.Polymorph.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Player;

namespace Content.Client.Polymorph.Systems;

public sealed class ChameleonProjectorSystem : SharedChameleonProjectorSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!; // DeadSpace14 ChameleonAlive

    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery; // DeadSpace14 ChameleonAlive

    public override void Initialize()
    {
        base.Initialize();

        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>(); // DeadSpace14 ChameleonAlive

        SubscribeLocalEvent<ChameleonDisguiseComponent, AfterAutoHandleStateEvent>(OnHandleState);

        SubscribeLocalEvent<ChameleonDisguisedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ChameleonDisguisedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChameleonDisguisedComponent, GetFlashEffectTargetEvent>(OnGetFlashEffectTargetEvent);
    }

    private void OnHandleState(Entity<ChameleonDisguiseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        CopyComp<HumanoidAppearanceComponent>(ent); // DeadSpace14 ChameleonAlive
        CopyComp<SpriteComponent>(ent);
        CopyComp<GenericVisualizerComponent>(ent);
        CopyComp<SolutionContainerVisualsComponent>(ent);
        CopyComp<BurnStateVisualsComponent>(ent);

        if (_humanoidQuery.TryComp(ent, out var humanoid)  // DeadSpace14 ChameleonAlive start
            && _spriteQuery.TryComp(ent, out var sprite))
        {
            _humanoid.UpdateSprite((ent.Owner, humanoid, sprite));
        }  // DeadSpace14 ChameleonAlive end

        // reload appearance to hopefully prevent any invisible layers
        if (_appearanceQuery.TryComp(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    private void OnStartup(Entity<ChameleonDisguisedComponent> ent, ref ComponentStartup args)
    {
        if (!_spriteQuery.TryComp(ent, out var sprite))
            return;

        ent.Comp.WasVisible = sprite.Visible;
        _sprite.SetVisible((ent.Owner, sprite), false);
    }

    private void OnShutdown(Entity<ChameleonDisguisedComponent> ent, ref ComponentShutdown args)
    {
        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent.Owner, sprite), ent.Comp.WasVisible);
    }

    private void OnGetFlashEffectTargetEvent(Entity<ChameleonDisguisedComponent> ent, ref GetFlashEffectTargetEvent args)
    {
        args.Target = ent.Comp.Disguise;
    }
}
