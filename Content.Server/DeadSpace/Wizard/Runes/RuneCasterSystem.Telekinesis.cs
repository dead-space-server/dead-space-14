// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private const float TelekinesisStop = 0.9f;

    private void ApplyTelekinesis(RuneCastContext ctx, RuneTelekinesisEffect effect)
    {
        var caster = _transform.GetMapCoordinates(ctx.Caster);
        var point = _transform.ToMapCoordinates(ctx.Point);
        if (point.MapId != caster.MapId)
            return;

        if (ctx.Target is { } mob && mob != ctx.Caster && !TerminatingOrDeleted(mob))
        {
            var pull = effect.PullSpeed * ctx.Power * DisplacedScale(mob, ctx);
            Touch(ctx, mob);
            Yank(mob, caster.Position, pull, ctx.Caster);
            return;
        }

        if (FindLooseItem(point, effect.GrabRadius) is { } item)
        {
            var flight = Yank(item, caster.Position, effect.PullSpeed * ctx.Power, ctx.Caster);
            var user = ctx.Caster;
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(flight + 0.1f), () =>
            {
                if (TerminatingOrDeleted(item) || TerminatingOrDeleted(user) || _container.IsEntityInContainer(item))
                    return;

                if (_interaction.InRangeUnobstructed(user, item, 2f))
                    _hands.TryPickupAnyHand(user, item, checkActionBlocker: false);
            });
            return;
        }

        if (_hands.GetActiveItem(ctx.Caster) is { } held && _hands.TryDrop(ctx.Caster, held, checkActionBlocker: false))
        {
            _throwing.TryThrow(held, ctx.Point, effect.HurlSpeed * ctx.Power, ctx.Caster, pushbackRatio: 0f, recoil: false);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rune-telekinesis-nothing"), ctx.Caster, ctx.Caster, PopupType.SmallCaution);
    }

    private float Yank(EntityUid uid, Vector2 to, float speed, EntityUid user)
    {
        var delta = to - _transform.GetWorldPosition(uid);
        var length = delta.Length();
        if (length <= TelekinesisStop)
            return 0f;

        var travel = delta / length * (length - TelekinesisStop);
        speed = MathF.Max(1f, speed);
        _throwing.TryThrow(uid, travel, speed, user, pushbackRatio: 0f, recoil: false);
        return travel.Length() / speed;
    }

    private EntityUid? FindLooseItem(MapCoordinates point, float radius)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(point, radius))
        {
            if (_container.IsEntityInContainer(item) || Transform(item).Anchored)
                continue;

            var distance = (_transform.GetWorldPosition(item) - point.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = item;
            bestDistance = distance;
        }

        return best;
    }
}
