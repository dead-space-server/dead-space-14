using Content.Server.DeadSpace.MartialArts;
using Content.Shared.Interaction.Events;
using Content.Shared.DeadSpace.MartialArts.Arkalyse;
using Content.Shared.Weapons.Melee;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Content.Shared.Speech.Muting;
using Robust.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp;

namespace Content.Shared.DeadSpace.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeArkalyse()
    {
        SubscribeLocalEvent<ArkalyseComponent, ArkalyseActionEvent>(OnArkalyseAction);
        SubscribeLocalEvent<ArkalyseComponent, MeleeHitEvent>(OnMeleeHitEvent);

        SubscribeLocalEvent<MartialArtsTrainingArkalyseComponent, UseInHandEvent>(UseBookArkalyse);
    }
    // Выдача способностей и превращение книги в пепел
    private void UseBookArkalyse(Entity<MartialArtsTrainingArkalyseComponent> ent, ref UseInHandEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        if (TryComp<SmokingCarpComponent>(args.User, out _))
            return;

        var userArkalyse = EnsureComp<ArkalyseComponent>(args.User);
        foreach (var actionId in userArkalyse.BaseArkalyse)
        {
            var actions = _action.AddAction(args.User, actionId);
            if (actions != null)
                userArkalyse.ArkalyseActionEntities.Add(actions.Value);
        }

        TransformToItem(ent, ent.Comp.ItemAfterLerning);

        var meleeWeaponComponent = EnsureComp<MeleeWeaponComponent>(args.User);
        meleeWeaponComponent.AttackRate = ent.Comp.AddAtackRate;

        args.Handled = true;
    }
    private void OnArkalyseAction(Entity<ArkalyseComponent> ent, ref ArkalyseActionEvent args)
    {
        if (_net.IsClient)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArkalyseActionComponent>(actionEnt, out var arkalyseActionComp))
            return;

        if (args.Handled)
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("active-martial-ability"), ent, ent);

        ent.Comp.SelectedCombo = arkalyseActionComp.List;
        ent.Comp.SelectedComboComp = arkalyseActionComp;
    }
    private void OnMeleeHitEvent(Entity<ArkalyseComponent> ent, ref MeleeHitEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.HitEntities.Count <= 0)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hitEntity))
                continue;

            DoHitArkalyse(ent, hitEntity);
        }
    }
    private void DoHitArkalyse(Entity<ArkalyseComponent> ent, EntityUid hitEntity)
    {
        if (ent.Comp.SelectedComboComp == null)
            return;

        var comboComp = ent.Comp.SelectedComboComp;

        switch (ent.Comp.SelectedCombo)
        {
            case ArkalyseList.DamageAtack:
                if (_net.IsClient)
                    return;
                DamageHit(ent, hitEntity, comboComp.DamageType, comboComp.HitDamage, comboComp.IgnoreResist, out _);
                SpawnAttachedTo(comboComp.EffectPunch, Transform(hitEntity).Coordinates);
                _audio.PlayPvs(comboComp.HitSound, ent, AudioParams.Default.WithVolume(3.0f));
                break;
            case ArkalyseList.StunAtack:
                if (_net.IsClient)
                    return;
                _audio.PlayPvs(comboComp.HitSound, ent, AudioParams.Default.WithVolume(0.5f));
                _stun.TryParalyze(hitEntity, TimeSpan.FromSeconds(comboComp.ParalyzeTime), true);
                SpawnAttachedTo(comboComp.EffectPunch, Transform(hitEntity).Coordinates);
                break;
            case ArkalyseList.MuteAtack:
                if (_net.IsClient)
                    return;
                EnsureComp<MutedComponent>(hitEntity);
                Timer.Spawn(TimeSpan.FromSeconds(comboComp.ParalyzeTime), () => { if (Exists(hitEntity)) RemComp<MutedComponent>(hitEntity); });

                DamageHit(ent, hitEntity, comboComp.DamageType, comboComp.HitDamage, comboComp.IgnoreResist, out _);
                _stamina.TakeStaminaDamage(hitEntity, comboComp.StaminaDamage);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ent.Comp.SelectedCombo = null;
        ent.Comp.SelectedComboComp = null;
    }
}
