// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Cargo.Components;
using Content.Server.Chat;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryMannequinSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryMannequinComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SurgeryMannequinComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<SurgeryMannequinComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(Entity<SurgeryMannequinComponent> ent, ref EntityTerminatingEvent args)
    {
        _status.TryRemoveStatusEffect(ent, SleepingSystem.StatusEffectForcedSleeping);
        RemComp<SleepingComponent>(ent);
    }

    private void OnMapInit(Entity<SurgeryMannequinComponent> ent, ref MapInitEvent args)
    {
        Make(ent);
    }

    public void Make(EntityUid uid)
    {
        EnsureComp<SurgeryMannequinComponent>(uid);
        RemComp<RespiratorComponent>(uid);
        RemComp<SatiationComponent>(uid);
        RemComp<ThermalRegulatorComponent>(uid);
        RemComp<BarotraumaComponent>(uid);
        RemComp<PerishableComponent>(uid);
        RemComp<EmoteOnDamageComponent>(uid);
        RemComp<MobPriceComponent>(uid);
        Hold(uid);
    }

    private void Hold(EntityUid uid)
    {
        if (!_mobs.IsDead(uid) && !HasComp<SleepingComponent>(uid))
            _status.TrySetStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping);

        if (!_standing.IsDown(uid))
            _standing.Down(uid, playSound: false, dropHeldItems: false, force: true);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 2f)
            return;

        _accumulator = 0f;
        var query = EntityQueryEnumerator<SurgeryMannequinComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            Hold(uid);
        }
    }

    private void OnVerbs(Entity<SurgeryMannequinComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<HumanoidAppearanceComponent>(ent, out var look))
            return;

        var target = ent.Owner;
        var speciesCategory = new VerbCategory("surgery-mannequin-verb-species", null);
        foreach (var species in _prototype.EnumeratePrototypes<SpeciesPrototype>())
        {
            if (!species.RoundStart || !SurgerySpeciesPrototype.CanOperate(_prototype, species.ID))
                continue;

            var id = species.ID;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(species.Name),
                Category = speciesCategory,
                Disabled = look.Species == id,
                Act = () => Swap(target, id),
            });
        }

        if (!_prototype.TryIndex(look.Species, out var own))
            return;

        var sexCategory = new VerbCategory("surgery-mannequin-verb-sex", null);
        foreach (var sex in own.Sexes)
        {
            var picked = sex;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString($"humanoid-profile-editor-sex-{picked.ToString().ToLowerInvariant()}-text"),
                Category = sexCategory,
                Disabled = look.Sex == picked,
                Act = () => _humanoid.SetSex(target, picked),
            });
        }
    }

    private void Swap(EntityUid old, string species)
    {
        if (TerminatingOrDeleted(old)
            || !_prototype.TryIndex<SpeciesPrototype>(species, out var proto)
            || TryComp<HumanoidAppearanceComponent>(old, out var look) && look.Species == species)
            return;

        var xform = Transform(old);
        var strap = CompOrNull<BuckleComponent>(old)?.BuckledTo;
        var spawned = Spawn(proto.Prototype, _transform.GetMapCoordinates(old, xform));
        _transform.SetLocalRotation(spawned, xform.LocalRotation);
        _meta.SetEntityName(spawned, Name(old));
        _meta.SetEntityDescription(spawned, Description(old));
        if (look != null && proto.Sexes.Contains(look.Sex))
            _humanoid.SetSex(spawned, look.Sex);

        Make(spawned);
        Del(old);

        if (strap is { } seat && !TerminatingOrDeleted(seat))
            _buckle.TryBuckle(spawned, null, seat, popup: false);
    }
}
