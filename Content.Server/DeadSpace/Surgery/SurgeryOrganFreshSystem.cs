// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryOrganFreshSystem : EntitySystem
{
    private const string Rules = "Default";
    private const float Tick = 2f;

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SurgeryGraftSystem _graft = default!;
    [Dependency] private readonly SurgeryOrganSystem _organs = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnRemoved);
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnAdded);
    }

    private void OnRemoved(Entity<OrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        Lost(ent, args.OldBody, args.OldPart);
        if (TerminatingOrDeleted(ent))
            return;

        _graft.Mark(ent, args.OldBody);
        if (TryComp<SurgeryOrganKindComponent>(ent, out var kind) && kind.Machine
            || !_prototype.TryIndex<SurgeryDecayPrototype>(Rules, out var rules))
            return;

        var brain = HasComp<BrainComponent>(ent);
        var fresh = EnsureComp<LimbFreshComponent>(ent);
        fresh.Total = brain ? rules.Brain : rules.Organ;
        fresh.ColdKeeps = !brain;
        var gone = TryComp<LimbBodyComponent>(args.OldBody, out var limbs) && limbs.Died is { } died
            ? (float) (_timing.CurTime - died).TotalSeconds
            : 0f;
        fresh.Left = MathF.Max(0f, fresh.Total - gone);
        fresh.Spent = fresh.Left <= 0f;
        Dirty(ent, fresh);
    }

    private void Lost(EntityUid organ, EntityUid body, EntityUid part)
    {
        if (HasComp<SurgeryKeepOrganComponent>(organ)
            || TerminatingOrDeleted(body)
            || CompOrNull<BodyPartComponent>(part)?.Body != body
            || !TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        var machine = TryComp<HumanoidAppearanceComponent>(body, out var look)
                      && SurgerySpeciesPrototype.IsMachine(_prototype, look.Species.Id);
        foreach (var kind in Enum.GetValues<LimbOrgan>())
        {
            var index = (int) kind;
            if ((limbs.Present & (LimbOrganMask) (1 << index)) == 0
                || !SurgeryOrganKindComponent.Matches(EntityManager, organ, kind.ToString(), machine))
                continue;

            if ((uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing)
                return;

            _limbs.ApplyOrganEdit(body, kind, new LimbOrganEdit
            {
                SetMissing = true,
                Missing = true,
                EyesGone = kind == LimbOrgan.Eyes ? 2 - limbs.EyesGone : 0,
            });
            _organs.Refresh(body);
            if (_limbs.IsVital(body, kind))
            {
                _limbs.ApplyEdit(body, LimbOrganHomes.Home(kind), new LimbEdit { SetHealth = true, Health = 0f });
                _limbs.KillByLimbs(body);
            }

            return;
        }
    }

    private void OnAdded(Entity<OrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RemComp<LimbFreshComponent>(ent);
    }

    public float Spoil(EntityUid organ)
    {
        return TryComp<LimbFreshComponent>(organ, out var fresh) ? fresh.Spoil : 0f;
    }

    public void MakeFresh(EntityUid organ)
    {
        if (TerminatingOrDeleted(organ)
            || TryComp<SurgeryOrganKindComponent>(organ, out var kind) && kind.Machine
            || !_prototype.TryIndex<SurgeryDecayPrototype>(Rules, out var rules))
            return;

        var brain = HasComp<BrainComponent>(organ);
        var fresh = EnsureComp<LimbFreshComponent>(organ);
        fresh.Total = brain ? rules.Brain : rules.Organ;
        fresh.ColdKeeps = !brain;
        fresh.Left = fresh.Total;
        fresh.Spent = false;
        Dirty(organ, fresh);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < Tick || !_prototype.TryIndex<SurgeryDecayPrototype>(Rules, out var rules))
            return;

        _accumulator = 0f;
        var query = EntityQueryEnumerator<LimbBodyComponent>();
        while (query.MoveNext(out var uid, out var limbs))
        {
            if (limbs.Died is not { } died || HasComp<UnrevivableComponent>(uid))
                continue;

            var index = (int) LimbOrgan.Brain;
            if ((limbs.Present & LimbOrganMask.Brain) == 0 || (uint) index >= (uint) limbs.Organs.Count || limbs.Organs[index].Missing)
                continue;

            var level = Math.Clamp((float) (_timing.CurTime - died).TotalSeconds / rules.Brain, 0f, 1f);
            var damage = level * LimbOrganRules.Rupture;
            if (limbs.Organs[index].Damage < damage)
            {
                _limbs.ApplyOrganEdit(uid, LimbOrgan.Brain, new LimbOrganEdit
                {
                    SetDamage = true,
                    Damage = damage,
                    SetRuptured = level >= 1f,
                    Ruptured = level >= 1f,
                });
            }

            if (level >= 1f)
                MarkRotten(uid, rules);
        }
    }

    public void MarkRotten(EntityUid body, SurgeryDecayPrototype? rules = null)
    {
        if (HasComp<UnrevivableComponent>(body))
            return;

        rules ??= _prototype.TryIndex<SurgeryDecayPrototype>(Rules, out var found) ? found : null;
        var unrevivable = EnsureComp<UnrevivableComponent>(body);
        unrevivable.Cloneable = true;
        if (rules != null)
            unrevivable.ReasonMessage = rules.RottenReason;
        Dirty(body, unrevivable);
    }
}
