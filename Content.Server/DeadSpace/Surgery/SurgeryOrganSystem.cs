// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed partial class SurgeryOrganSystem : EntitySystem
{
    private const string Ethanol = "Ethanol";

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryOrganStateComponent, EatingDoAfterEvent>(OnEating, before: [typeof(IngestionSystem)]);
    }

    public void Refresh(EntityUid body)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        var traits = new SurgeryOrganTraits();
        var poison = 0f;
        foreach (var role in _prototype.EnumeratePrototypes<SurgeryOrganRolePrototype>())
        {
            if (!Enum.TryParse<LimbOrgan>(role.Organ, out var organ))
                continue;

            var index = (int) organ;
            var missing = (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
            if (missing)
                traits.Merge(role.Missing);
            else if (Installed(body, role.Organ) is { } installed)
                traits.Merge(installed);

            poison = MathF.Max(poison, role.AlcoholPoison);
        }

        if (traits.Plain)
        {
            RemComp<SurgeryOrganStateComponent>(body);
            return;
        }

        var state = EnsureComp<SurgeryOrganStateComponent>(body);
        state.Traits = traits;
        state.AlcoholPoison = poison;
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        var step = _accumulator;
        _accumulator = 0f;
        var query = EntityQueryEnumerator<SurgeryOrganStateComponent>();
        while (query.MoveNext(out var uid, out var state))
        {
            var traits = state.Traits;
            if (TryComp<SatiationComponent>(uid, out var satiation))
            {
                if (traits.HungerDrain != 0f)
                    _satiation.ModifyValue((uid, satiation), SatiationSystem.Hunger, -traits.HungerDrain * step);
                if (traits.ThirstDrain != 0f)
                    _satiation.ModifyValue((uid, satiation), SatiationSystem.Thirst, -traits.ThirstDrain * step);
            }

            if (traits.Alcohol != 1f)
                Alcohol(uid, traits.Alcohol, state.AlcoholPoison * step);
        }
    }

    private void Alcohol(EntityUid uid, float factor, float rate)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream)
            || !_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out var bloodEntity, out var blood))
            return;

        var ethanol = blood.GetTotalPrototypeQuantity(Ethanol).Float();
        if (ethanol <= 0f)
            return;

        if (factor > 1f)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Poison"] = ethanol * rate * (factor - 1f);
            _damage.TryChangeDamage(uid, damage, true, false);
            return;
        }

        _solutions.RemoveReagent(bloodEntity.Value, Ethanol, FixedPoint2.New(ethanol * (1f - factor) * 0.1f));
    }

    private void OnEating(Entity<SurgeryOrganStateComponent> ent, ref EatingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } food || !TryComp<EdibleComponent>(food, out var edible))
            return;

        var drink = edible.Edible == IngestionSystem.Drink;
        if (drink ? ent.Comp.Traits.Drink : ent.Comp.Traits.Eat)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(drink ? "surgery-organ-no-drink" : "surgery-organ-no-eat"), ent, ent);
    }

    private SurgeryOrganTraits? Installed(EntityUid body, string organ)
    {
        foreach (var part in _body.GetBodyOrgans(body))
        {
            if (MetaData(part.Id).EntityPrototype is not { } proto
                || !proto.ID.Contains(organ, StringComparison.OrdinalIgnoreCase))
                continue;

            return TryComp<SurgeryOrganTraitsComponent>(part.Id, out var traits) ? traits.Traits : null;
        }

        return null;
    }
}

[RegisterComponent]
public sealed partial class SurgeryOrganStateComponent : Component
{
    public SurgeryOrganTraits Traits = new();

    public float AlcoholPoison;
}
