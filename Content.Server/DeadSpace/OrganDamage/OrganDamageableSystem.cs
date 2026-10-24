// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.OrganDamage;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Rejuvenate;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.OrganDamage;

public sealed class OrganDamageableSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, DamageChangedEvent>(OnBodyDamaged);
        SubscribeLocalEvent<OrganDamageableComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OrganDamageableComponent, OrganComponent>();
        while (query.MoveNext(out var organUid, out var organDamage, out var organ))
        {
            if (!organDamage.Dead || organDamage.DeadBodyDamagePerSecond.Count == 0)
                continue;

            if (organ.Body is not { Valid: true } body)
                continue;

            var spec = new DamageSpecifier();
            foreach (var (typeId, perSecond) in organDamage.DeadBodyDamagePerSecond)
            {
                if (perSecond <= 0f)
                    continue;

                spec.DamageDict[typeId] = FixedPoint2.New(perSecond * frameTime);
            }

            spec.TrimZeros();
            if (spec.Empty)
                continue;

            _damageable.TryChangeDamage(body, spec, ignoreResistances: true, interruptsDoAfters: false, origin: organUid);
        }
    }

    private void OnBodyDamaged(Entity<BodyComponent> body, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        if (args.Origin != null && TryComp<OrganDamageableComponent>(args.Origin, out var originOrgan) && originOrgan.Dead)
            return;

        var organs = _body.GetBodyOrganEntityComps<OrganDamageableComponent>((body.Owner, body.Comp));
        if (organs.Count == 0)
            return;

        var recordWounds = args.DamageIncreased;

        foreach (var organ in organs)
        {
            if (organ.Comp1.Dead && args.DamageIncreased)
                continue;

            var spec = new DamageSpecifier();
            foreach (var (typeId, amount) in args.DamageDelta.DamageDict)
            {
                if (!organ.Comp1.DamageMultipliers.TryGetValue(typeId, out var mult) || mult <= FixedPoint2.Zero)
                    continue;

                spec.DamageDict[typeId] = amount * mult;
            }

            spec.TrimZeros();
            if (spec.Empty)
                continue;

            ApplyOrganDamage((organ.Owner, organ.Comp1), spec, args.Origin, body.Owner, recordWounds);
        }
    }

    public void ApplyOrganDamage(
        Entity<OrganDamageableComponent> organ,
        DamageSpecifier spec,
        EntityUid? origin,
        EntityUid? body,
        bool recordWounds)
    {
        var comp = organ.Comp;
        var total = FixedPoint2.Zero;

        foreach (var (typeId, amount) in spec.DamageDict)
        {
            total += amount;

            if (recordWounds && amount > FixedPoint2.Zero)
                RecordWound(organ, typeId, amount, origin);
        }

        if (total == FixedPoint2.Zero)
            return;

        var oldDamage = comp.Damage;
        comp.Damage = FixedPoint2.Clamp(comp.Damage + total, FixedPoint2.Zero, comp.MaxDamage);

        if (comp.Damage == oldDamage)
            return;

        UpdateAppearance(organ, comp);

        if (CrossedThreshold(oldDamage, comp.Damage, comp.MaxDamage) && !comp.Dead)
        {
            comp.Dead = true;
            UpdateAppearance(organ, comp);
            Dirty(organ, comp);

            var destroyedEv = new OrganDestroyedEvent(organ, body);
            RaiseLocalEvent(organ.Owner, ref destroyedEv);
            if (body != null)
                RaiseLocalEvent(body.Value, ref destroyedEv);
            return;
        }

        Dirty(organ, comp);
        var damagedEv = new OrganDamagedEvent(organ, body, origin);
        RaiseLocalEvent(organ.Owner, ref damagedEv);
    }

    private static bool CrossedThreshold(FixedPoint2 oldDamage, FixedPoint2 newDamage, FixedPoint2 maxDamage)
    {
        return oldDamage < maxDamage && newDamage >= maxDamage;
    }

    private void RecordWound(Entity<OrganDamageableComponent> organ, string typeId, FixedPoint2 amount, EntityUid? origin)
    {
        var comp = organ.Comp;
        var source = ResolveSource(origin);
        var now = _timing.CurTime;

        foreach (var wound in comp.Wounds)
        {
            if (wound.DamageType == typeId && wound.Source == source)
            {
                wound.Merge(amount, now);
                return;
            }
        }

        comp.Wounds.Add(new OrganWound(Guid.NewGuid(), now, typeId, amount, source));

        while (comp.Wounds.Count > comp.MaxWounds)
            comp.Wounds.RemoveAt(0);
    }

    private string? ResolveSource(EntityUid? origin)
    {
        if (OrganDamageContext.CurrentReagent is { } reagent)
            return reagent;

        if (origin == null)
            return null;

        if (TryComp<HandsComponent>(origin, out var hands) &&
            _hands.GetActiveItem((origin.Value, hands)) is { Valid: true } item &&
            TryComp<MetaDataComponent>(item, out var itemMeta))
        {
            return itemMeta.EntityName;
        }

        if (TryComp<MetaDataComponent>(origin, out var meta))
            return meta.EntityName;

        return null;
    }

    public void RestoreOrgan(Entity<OrganDamageableComponent> organ)
    {
        var comp = organ.Comp;
        comp.Damage = FixedPoint2.Zero;
        comp.Dead = false;
        comp.Wounds.Clear();

        UpdateAppearance(organ, comp);
        Dirty(organ, comp);

        var ev = new OrganRestoredEvent(organ, TryComp<OrganComponent>(organ.Owner, out var organComp) ? organComp.Body : null);
        RaiseLocalEvent(organ.Owner, ref ev);
    }

    private void OnRejuvenate(Entity<OrganDamageableComponent> organ, ref RejuvenateEvent args)
    {
        RestoreOrgan(organ);
    }

    private void UpdateAppearance(Entity<OrganDamageableComponent> organ, OrganDamageableComponent comp)
    {
        var percent = comp.MaxDamage > FixedPoint2.Zero
            ? (byte) Math.Clamp((comp.Damage / comp.MaxDamage).Float() * 100f, 0f, 100f)
            : (byte) 0;

        _appearance.SetData(organ.Owner, OrganDamageVisuals.DamagePercent, percent);
        _appearance.SetData(organ.Owner, OrganDamageVisuals.Dead, comp.Dead);
    }
}
