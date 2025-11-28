// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Server.DeadSpace.MonkeyKing.Components;
using Content.Shared.Movement.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Speech.Components;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MonkeyKing;

public sealed class MonkeyServantSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonkeyServantComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<MonkeyServantComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MonkeyServantComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime <= component.TimeUntilEndBuff && component.IsBuffed)
                UnBuff(uid, component);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, MonkeyServantComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.SpeedMulty, component.SpeedMulty);
    }

    private void OnDamage(EntityUid uid, MonkeyServantComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        if (component.IsBuffed)
        {

            var damage = args.DamageDelta * component.GetDamageMulty;
            _damageable.TryChangeDamage(uid, -damage);
        }
    }

    public void Buff(EntityUid uid, MonkeyServantComponent component, TimeSpan duration, float speed, float getDamage)
    {
        component.SpeedMulty = speed;
        component.GetDamageMulty = getDamage;

        if (TryComp<VocalComponent>(uid, out var vocal) && _proto.TryIndex(ScreamEmote, out var scream) && _proto.TryIndex(vocal.EmoteSounds, out var emoteSounds))
        {
            _chat.TryPlayEmoteSound(uid, emoteSounds, scream);
            _chat.TryEmoteWithoutChat(uid, ScreamEmote);
        }

        _movement.RefreshMovementSpeedModifiers(uid);

        component.TimeUntilEndBuff = _gameTiming.CurTime + duration;
        component.IsBuffed = true;
    }

    public void UnBuff(EntityUid uid, MonkeyServantComponent component)
    {
        component.SpeedMulty = 1f;
        component.GetDamageMulty = 1f;

        _movement.RefreshMovementSpeedModifiers(uid);
        component.IsBuffed = false;
    }

}
