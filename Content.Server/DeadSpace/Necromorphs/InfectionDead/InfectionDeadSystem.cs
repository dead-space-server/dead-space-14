// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Content.Shared.DeadSpace.Necromorphs.PlasmaCutter;

namespace Content.Server.DeadSpace.Necromorphs.InfectionDead;

public sealed partial class InfectionDeadSystem : SharedInfectionDeadSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private NecromorfSystem _necromorfSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfectionDeadComponent, InfectionDeadSymptomsEvent>(GetDamage);
        SubscribeLocalEvent<InfectionDeadComponent, InfectionNecroficationEvent>(OnState);
    }

    private void GetDamage(EntityUid uid, InfectionDeadComponent component, ref InfectionDeadSymptomsEvent args)
    {
        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Cellular", 15f);
        _damage.TryChangeDamage(uid, dspec, true, false);
    }

    private void OnState(EntityUid uid, InfectionDeadComponent component, InfectionNecroficationEvent args)
    {
        if (HasComp<NecromorphMissingHeadComponent>(uid))
            return;

        if (!_mobState.IsDead(uid))
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent))
            return;

        var isAnimal = false;

        if (HasComp<HumanoidAppearanceComponent>(uid))
            isAnimal = false;
        else
            isAnimal = true;

        var necromorf = GetRandomNecromorfPrototypeId(isAnimal);

        if (TryComp<NecromorfAfterInfectionComponent>(uid, out var necroComponent))
            necromorf = necroComponent.NecroPrototype;

        if (necromorf != null)
            _necromorfSystem.Necrofication(uid, necromorf, component.StrainData, mobStateComponent);

        RemComp<InfectionDeadComponent>(uid);
    }

}
