using Content.Shared.DeadSpace.Triggers.Components;
using Content.Shared.Trigger;
using Content.Shared.Whistle;

namespace Content.Shared.DeadSpace.Triggers.Systems;

public sealed partial class WhistleOnTriggerSystem : TriggerOnXSystem
{
    [Dependency] private WhistleSystem _whistle = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhistleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<WhistleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;
        if (!TryComp<WhistleComponent>(ent, out var whistle))
            return;
        _whistle.TryMakeLoudWhistle(ent, args.User, whistle);
        args.Handled = true;
    }
}