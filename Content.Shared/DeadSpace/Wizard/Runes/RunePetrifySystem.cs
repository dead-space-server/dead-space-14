// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Weapons.Parry;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Rotatable;
using Content.Shared.Movement.Events;
using Content.Shared.Speech;
using Content.Shared.Throwing;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed class RunePetrifySystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RunePetrifiedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RunePetrifiedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RunePetrifiedComponent, UpdateCanMoveEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, ChangeDirectionAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, UseAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, PickupAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, GettingPickedUpAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, DropAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, AttackAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, ThrowAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, SpeakAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, EmoteAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, InteractionAttemptEvent>(OnInteract);
        SubscribeLocalEvent<RunePetrifiedComponent, ConsciousAttemptEvent>(OnConscious);
        SubscribeLocalEvent<RunePetrifiedComponent, IsEquippingAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, IsUnequippingAttemptEvent>(OnCancel);
        SubscribeLocalEvent<RunePetrifiedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RuneTimeHeldComponent, BeforeDamageChangedEvent>(OnHeldDamage);
        SubscribeLocalEvent<RuneTimeHeldComponent, BeforeMeleeDamageEvent>(OnHeldMelee);
    }

    private void OnHeldMelee(Entity<RuneTimeHeldComponent> ent, ref BeforeMeleeDamageEvent args)
    {
        if (_net.IsClient)
            args.Cancelled = true;
    }

    private void OnExamined(Entity<RunePetrifiedComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Description is { } description)
            args.PushMarkup(Loc.GetString(description));
    }

    private void OnConscious(Entity<RunePetrifiedComponent> ent, ref ConsciousAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHeldDamage(Entity<RuneTimeHeldComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (_net.IsClient && !args.Damage.Empty)
            args.Cancelled = true;
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
