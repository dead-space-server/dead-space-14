using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling.Components;
using Content.Shared.DeadSpace.Changeling.Components; // DS14
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Changeling.Systems;

public sealed partial class RegenerativeStasisSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!; // DS14
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    // DS14-start: pre-v288 RobustToolbox has no action-event relay or subscription source generator.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RegenerativeStasisActionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionsComponent, MobStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<RegenerativeStasisActionComponent, ChangelingStasisActionEvent>(OnStasisUse);
    }
    // DS14-end

    private void OnMapInit(Entity<RegenerativeStasisActionComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.InitialName = MetaData(ent).EntityName;
        ent.Comp.InitialDescription = MetaData(ent).EntityDescription;
        Dirty(ent);
    }

    // DS14-start: relay the body state change to its stasis action using the legacy action collection.
    private void OnStateChanged(Entity<ActionsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        foreach (var action in ent.Comp.Actions)
        {
            if (TryComp<RegenerativeStasisActionComponent>(action, out var stasis) && stasis.IsInStasis)
            {
                CancelStasis((action, stasis));
                RestoreStrip(ent.Owner);
            }
        }
    }
    // DS14-end

    private void OnStasisUse(Entity<RegenerativeStasisActionComponent> ent, ref ChangelingStasisActionEvent args)
    {
        if (ent.Comp.IsInStasis)
        {
            ExitStasis((ent, ent.Comp), args.Performer);
            args.Handled = true; //Only handle when exiting, as we don't need the useDelay otherwise.
            return;
        }

        EnterStasis((ent, ent.Comp), args.Performer);
    }

    /// <summary>
    /// Enter the stasis and set the action cooldown depending on the damage you have taken.
    /// </summary>
    public void EnterStasis(Entity<RegenerativeStasisActionComponent?> ent, EntityUid target)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (ent.Comp.IsInStasis)
            return;

        // If going from Alive to Dead fake a death gasp.
        // If going from Critical to Dead then DeathGaspSystem is already doing this,
        // so we don't want to do it twice.
        if (_mobs.IsAlive(target))
        {
            var deathgasp = new RequestDeathgaspEvent();
            RaiseLocalEvent(target, ref deathgasp);
        }

        // Die temporarily until we revive.
        // Ghosting will be blocked while in stasis.
        if (!_mobs.IsDead(target))
            _mobs.ChangeMobState(target, MobState.Dead);

        _popup.PopupPredicted(Loc.GetString("changeling-stasis-enter"), null, target, target, PopupType.MediumCaution); // DS14

        // DS14-start
        if (HasComp<ChangelingCocoonAbilityComponent>(target) &&
            TryComp<HandsComponent>(target, out var cocoonHands) &&
            cocoonHands.CanBeStripped)
        {
            ent.Comp.CocoonActive = true;
            _hands.SetCanBeStripped((target, cocoonHands), false);
            _popup.PopupPredicted(Loc.GetString("changeling-stasis-cocoon-enter"), null, target, target); // DS14
            Dirty(ent);
        }

        ent.Comp.IsInStasis = true;
        Dirty(ent);

        var stasisDuration = ent.Comp.MinStasisCooldown;

        var totalDamage = TryComp<DamageableComponent>(target, out var damageable)
            ? (double) damageable.TotalDamage
            : 0d;
        stasisDuration += ent.Comp.BonusCooldownPerDamage * totalDamage;
        stasisDuration = new TimeSpan(Math.Clamp(stasisDuration.Ticks, ent.Comp.MinStasisCooldown.Ticks, ent.Comp.MaxStasisCooldown.Ticks)); // No clamp method for TimeSpans

        _metaData.SetEntityName(ent, Loc.GetString("changeling-stasis-active-name"));
        _metaData.SetEntityDescription(ent, Loc.GetString("changeling-stasis-active-desc"));

        _actions.SetToggled(ent.Owner, ent.Comp.IsInStasis);
        _actions.SetCooldown(ent.Owner, stasisDuration);
    }

    /// <summary>
    /// Exit the stasis and heal all damage and bloodloss.
    /// TODO: Maybe add a some sort of rejuvenate lite so that we can also heal some status effects?
    /// </summary>
    public void ExitStasis(Entity<RegenerativeStasisActionComponent?> ent, EntityUid target)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.IsInStasis)
            return;

        // Heal all damage.
        _damage.ClearAllDamage(target);

        // Heal bloodloss and stop bleeding.
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _bloodstream.TryRegulateBloodLevel((target, bloodstream), bloodstream.BloodReferenceSolution.MaxVolume);
            _bloodstream.TryModifyBleedAmount((target, bloodstream), -bloodstream.BleedAmount);
        }

        // Revive.
        _mobs.ChangeMobState(target, MobState.Alive);

        _popup.PopupPredicted(Loc.GetString("changeling-stasis-exit"), Loc.GetString("changeling-stasis-exit-others", ("user", Identity.Entity(target, EntityManager))), target, target, PopupType.MediumCaution);
        _audio.PlayPredicted(ent.Comp.ExitSound, target, target);

        // DS14-start
        ent.Comp.IsInStasis = false;
        ent.Comp.CocoonActive = false;
        RestoreStrip(target);
        // DS14-end
        Dirty(ent);

        if (ent.Comp.InitialName != null)
            _metaData.SetEntityName(ent, ent.Comp.InitialName);
        if (ent.Comp.InitialDescription != null)
            _metaData.SetEntityDescription(ent, ent.Comp.InitialDescription);

        _actions.SetToggled(ent.Owner, ent.Comp.IsInStasis);
    }

    /// <summary>
    /// Cancel the stasis without healing.
    /// </summary>
    public void CancelStasis(Entity<RegenerativeStasisActionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.IsInStasis)
            return;

        ent.Comp.IsInStasis = false;
        ent.Comp.CocoonActive = false;
        Dirty(ent);

        if (ent.Comp.InitialName != null)
            _metaData.SetEntityName(ent, ent.Comp.InitialName);
        if (ent.Comp.InitialDescription != null)
            _metaData.SetEntityDescription(ent, ent.Comp.InitialDescription);

        _actions.SetToggled(ent.Owner, ent.Comp.IsInStasis);
    }

    // DS14-start
    private void RestoreStrip(EntityUid target)
    {
        if (TryComp<HandsComponent>(target, out var hands) && !hands.CanBeStripped)
            _hands.SetCanBeStripped((target, hands), true);
    }
    // DS14-end
}

/// <summary>
/// Action event for entering/leaving the stasis.
/// </summary>
public sealed partial class ChangelingStasisActionEvent : InstantActionEvent;
