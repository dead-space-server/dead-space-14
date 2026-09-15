using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.StatusEffectNew;

/// <summary>
/// Handler for <see cref="ExaminableStatusEffectComponent"/>.
/// </summary>
public sealed partial class ExaminableStatusEffectSystem : EntitySystem
{
    // DS14-start: current engine baseline uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExaminableStatusEffectComponent, StatusEffectRelayedEvent<ExaminedEvent>>(OnExaminedEvent);
    }
    // DS14-end

    private void OnExaminedEvent(Entity<ExaminableStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ExaminedEvent> args)
    {
        using (args.Args.PushGroup(nameof(ExaminableStatusEffectSystem)))
        {
            args.Args.PushMarkup(Loc.GetString(ent.Comp.MessageId, ("target", Identity.Entity(args.AppliedTo, EntityManager))));
        }
    }
}
