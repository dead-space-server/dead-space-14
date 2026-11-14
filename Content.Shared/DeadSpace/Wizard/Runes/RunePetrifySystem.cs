// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.Emoting;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Speech;
using Content.Shared.Throwing;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed class RunePetrifySystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RunePetrifiedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RunePetrifiedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RunePetrifiedComponent, UpdateCanMoveEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, ChangeDirectionAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, UseAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, PickupAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, DropAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, AttackAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, ThrowAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, SpeakAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, EmoteAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, InteractionAttemptEvent>(OnInteract);
    }

    private void OnStartup(Entity<RunePetrifiedComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<RunePetrifiedComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnCancel(EntityUid uid, RunePetrifiedComponent comp, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnInteract(Entity<RunePetrifiedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
