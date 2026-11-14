// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Stabilizing serum: injected into a legion soul, it stops the soul from decaying into an inert husk. The soul
/// itself (<see cref="LavalandRegenerativeCoreComponent"/>), while it has not faded, heals all of someone's wounds.
/// </summary>
public sealed class LavalandSoulStabilizerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandSoulStabilizerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LavalandStabilizableComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LavalandRegenerativeCoreComponent, UseInHandEvent>(OnCoreUseInHand);
        SubscribeLocalEvent<LavalandRegenerativeCoreComponent, AfterInteractEvent>(OnCoreAfterInteract);
    }

    private void OnCoreUseInHand(Entity<LavalandRegenerativeCoreComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        UseCore(ent, args.User, args.User);
    }

    private void OnCoreAfterInteract(Entity<LavalandRegenerativeCoreComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        UseCore(ent, args.User, target);
    }

    /// <summary>The soul's power closes the wounds at once; the dead it does not bring back.</summary>
    private void UseCore(Entity<LavalandRegenerativeCoreComponent> ent, EntityUid user, EntityUid target)
    {
        if (!TryComp<MobStateComponent>(target, out var mob) || _mobState.IsDead(target, mob))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-regenerative-core-dead"), target, user);
            return;
        }

        if (ent.Comp.Heal is { } heal)
            _damageable.TryChangeDamage(target, heal, true, origin: user);
        else
            _damageable.ClearAllDamage(target);
        _bloodstream.TryModifyBleedAmount(target, -1000f);
        _audio.PlayPvs(ent.Comp.HealSound, target);
        if (user == target)
            _popup.PopupEntity(Loc.GetString("lavaland-regenerative-core-self"), target, user, PopupType.Medium);
        else
            _popup.PopupEntity(Loc.GetString("lavaland-regenerative-core-other", ("target", target)), target, PopupType.Medium);
        QueueDel(ent);
    }

    private void OnAfterInteract(Entity<LavalandSoulStabilizerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!TryComp<LavalandStabilizableComponent>(target, out var stabilizable))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-invalid"), target, args.User);
            return;
        }

        if (stabilizable.Stabilized)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-already"), target, args.User);
            return;
        }

        RemComp<PerishableComponent>(target);
        RemComp<RotIntoComponent>(target);
        stabilizable.Stabilized = true;
        _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-success"), target, args.User);
        QueueDel(ent);
    }

    private void OnExamine(Entity<LavalandStabilizableComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Stabilized
            ? "lavaland-stabilizer-examine-stable"
            : "lavaland-stabilizer-examine-decaying"));
    }
}
