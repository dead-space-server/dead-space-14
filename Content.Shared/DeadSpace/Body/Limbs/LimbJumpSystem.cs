// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed class LimbJumpSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, LimbJumpRefreshEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<BodyComponent> ent, ref LimbJumpRefreshEvent args)
    {
        Refresh(ent.Owner, ent.Comp);
    }

    private void Refresh(EntityUid body, BodyComponent bodyComp)
    {
        if (_net.IsClient)
            return;

        var distance = 0f;
        var speed = 0f;
        var collide = false;
        EntProtoId action = "ActionGravityJump";
        SoundSpecifier? sound = null;
        var any = false;
        foreach (var (part, partComp) in _body.GetBodyChildren(body, bodyComp))
        {
            if (partComp.PartType != BodyPartType.Leg || !TryComp<LimbJumpComponent>(part, out var jump))
                continue;

            any = true;
            distance += jump.Distance;
            speed += jump.Speed;
            collide |= jump.CanCollide;
            action = jump.Action;
            sound ??= jump.Sound;
        }

        if (!any)
        {
            RemComp<JumpAbilityComponent>(body);
            return;
        }

        var ability = EnsureComp<JumpAbilityComponent>(body);
        ability.JumpDistance = distance;
        ability.JumpThrowSpeed = speed;
        ability.CanCollide = collide;
        ability.RequireUnobstructedPath = true;
        ability.Action = action;
        ability.JumpSound = sound;
        Dirty(body, ability);
    }
}

[ByRefEvent]
public readonly record struct LimbJumpRefreshEvent;
