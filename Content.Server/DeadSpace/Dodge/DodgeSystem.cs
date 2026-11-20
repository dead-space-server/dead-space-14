// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Dodge;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Dodge;

public sealed class DodgeSystem : EntitySystem
{
    [Dependency] private readonly SharedDodgeSystem _dodge = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DodgeComponent, PreventCollideEvent>(OnProjectileCollision);
        SubscribeLocalEvent<DodgeComponent, HitscanDodgeAttemptEvent>(OnHitscanDodge);
    }

    private void OnProjectileCollision(Entity<DodgeComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled ||
            !TryComp<ProjectileComponent>(args.OtherEntity, out var projectile) ||
            projectile.ProjectileSpent ||
            !TryDodge(ent, projectile.Shooter))
        {
            return;
        }

        args.Cancelled = true;
    }

    private void OnHitscanDodge(Entity<DodgeComponent> ent, ref HitscanDodgeAttemptEvent args)
    {
        if (!args.Cancelled && TryDodge(ent, args.Data.Shooter ?? args.Data.Gun))
            args.Cancelled = true;
    }

    public bool TryDodge(Entity<DodgeComponent> ent, EntityUid? shooter)
    {
        if (shooter == ent.Owner)
            return false;

        if (_dodge.IsActive(ent.Owner) && ent.Comp.StaminaDodgeMode)
        {
            if (!TryComp<StaminaComponent>(ent.Owner, out var stamina) ||
                !_stamina.TryTakeStamina(
                    ent.Owner,
                    ent.Comp.GuaranteedStaminaCost,
                    stamina,
                    source: ent.Owner,
                    visual: false,
                    ignoreResist: true))
            {
                ShowNoStamina(ent);
                return false;
            }

            PlayDodgeVisuals(ent.Owner);
            return true;
        }

        if (_random.NextFloat() >= _dodge.GetChance(ent))
            return false;

        PlayDodgeVisuals(ent.Owner);
        return true;
    }

    private void ShowNoStamina(Entity<DodgeComponent> ent)
    {
        if (ent.Comp.NoStaminaPopup is not { } popup)
            return;

        var curTime = _timing.CurTime;
        if (curTime < ent.Comp.NextStaminaPopup)
            return;

        _popup.PopupEntity(Loc.GetString(popup), ent.Owner, ent.Owner, PopupType.SmallCaution);
        ent.Comp.NextStaminaPopup = curTime + TimeSpan.FromSeconds(MathF.Max(0f, ent.Comp.NoStaminaPopupCooldown));
    }

    private void PlayDodgeVisuals(EntityUid uid)
    {
        if (!TryComp<DodgeVisualComponent>(uid, out var visual))
            return;

        _jittering.DoJitter(
            uid,
            TimeSpan.FromSeconds(visual.JitterDuration),
            true,
            visual.JitterAmplitude,
            visual.JitterFrequency,
            true);
        RaiseNetworkEvent(new DodgeEvent(GetNetEntity(uid)));
    }
}
