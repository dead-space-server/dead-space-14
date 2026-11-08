// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Cloning;

public sealed class CloningLimbSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public bool IsRemains(EntityUid uid)
    {
        return IsHead(uid) || IsBrain(uid);
    }

    public bool IsBrain(EntityUid uid)
    {
        return HasComp<BrainComponent>(uid)
               && TryComp<OrganComponent>(uid, out var organ)
               && organ.Body == null
               && !(_containers.TryGetContainingContainer((uid, null, null), out var container) && HasComp<BodyPartComponent>(container.Owner));
    }

    public bool IsHead(EntityUid uid)
    {
        return TryComp<BodyPartComponent>(uid, out var part)
               && part.PartType == BodyPartType.Head
               && part.Body == null
               && TryComp<LimbFleshComponent>(uid, out var flesh)
               && flesh.Species.Length > 0;
    }

    public bool TryGetMind(EntityUid occupant, out EntityUid mindId, [NotNullWhen(true)] out MindComponent? mind)
    {
        if (_mind.TryGetMind(occupant, out mindId, out mind))
            return true;

        foreach (var head in Heads(occupant))
        {
            foreach (var organ in _body.GetPartOrgans(head))
            {
                if (HasComp<BrainComponent>(organ.Id) && _mind.TryGetMind(organ.Id, out mindId, out mind))
                    return true;
            }
        }

        return false;
    }

    public EntityUid Source(EntityUid occupant)
    {
        if (IsHead(occupant)
            && Comp<LimbFleshComponent>(occupant).Source is { } body
            && !TerminatingOrDeleted(body)
            && HasComp<HumanoidAppearanceComponent>(body))
            return body;

        return occupant;
    }

    private List<EntityUid> Heads(EntityUid occupant)
    {
        var heads = new List<EntityUid>();
        if (IsHead(occupant))
        {
            heads.Add(occupant);
            return heads;
        }

        var query = EntityQueryEnumerator<LimbFleshComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var flesh, out var part))
        {
            if (flesh.Source == occupant && part.PartType == BodyPartType.Head && part.Body == null)
                heads.Add(uid);
        }

        return heads;
    }
}
