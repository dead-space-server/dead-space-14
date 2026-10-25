// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Nutrition;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryXrayMarkPillSystem : EntitySystem
{
    private static readonly EntProtoId Shard = "SurgeryXrayShard";

    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SurgeryCavitySystem _cavity = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryXrayMarkPillComponent, FullyEatenEvent>(OnEaten);
    }

    private void OnEaten(Entity<SurgeryXrayMarkPillComponent> ent, ref FullyEatenEvent args)
    {
        if (!TryComp<LimbBodyComponent>(args.User, out _))
            return;

        switch (ent.Comp.Kind)
        {
            case SurgeryXrayMarkKind.Crack:
                MarkBones(args.User, LimbWoundRules.StrainBone, 0f);
                break;
            case SurgeryXrayMarkKind.ClosedFracture:
                MarkBones(args.User, LimbWoundRules.BreakBone, 0f);
                break;
            case SurgeryXrayMarkKind.OpenFracture:
                MarkBones(args.User, LimbWoundRules.BreakBone, LimbWoundRules.StrainFlesh);
                break;
            case SurgeryXrayMarkKind.Bleed:
                foreach (var zone in Zones)
                {
                    _limbs.ApplyEdit(args.User, zone, new LimbEdit
                    {
                        SetBleed = true,
                        Bleed = 2f,
                    });
                }
                break;
            case SurgeryXrayMarkKind.Rupture:
                Rupture(args.User, LimbOrgan.Brain);
                Rupture(args.User, LimbOrgan.Eyes);
                Rupture(args.User, LimbOrgan.Heart);
                Rupture(args.User, LimbOrgan.Lungs);
                Rupture(args.User, LimbOrgan.Stomach);
                Rupture(args.User, LimbOrgan.Liver);
                Rupture(args.User, LimbOrgan.Kidneys);
                Rupture(args.User, LimbOrgan.Intestines);
                break;
            case SurgeryXrayMarkKind.Embedded:
                foreach (var zone in Zones)
                {
                    _limbs.ApplyEdit(args.User, zone, new LimbEdit
                    {
                        SetEmbedded = true,
                        Embedded = true,
                    });
                }
                break;
            case SurgeryXrayMarkKind.Foreign:
                Lodge(args.User, LimbZone.Head);
                Lodge(args.User, LimbZone.Groin);
                Lodge(args.User, LimbZone.Torso);
                break;
        }
    }

    private static readonly LimbZone[] Zones =
    {
        LimbZone.Head,
        LimbZone.Torso,
        LimbZone.Groin,
        LimbZone.ArmLeft,
        LimbZone.ArmRight,
        LimbZone.HandLeft,
        LimbZone.HandRight,
        LimbZone.LegLeft,
        LimbZone.LegRight,
        LimbZone.FootLeft,
        LimbZone.FootRight,
    };

    private void MarkBones(EntityUid user, float bone, float flesh)
    {
        foreach (var zone in Zones)
        {
            _limbs.ApplyEdit(user, zone, new LimbEdit
            {
                SetBone = true,
                Bone = bone,
                SetFlesh = true,
                Flesh = flesh,
            });
        }
    }

    private void Rupture(EntityUid user, LimbOrgan organ)
    {
        _limbs.ApplyOrganEdit(user, organ, new LimbOrganEdit
        {
            SetRuptured = true,
            Ruptured = true,
        });
    }

    private void Lodge(EntityUid user, LimbZone zone)
    {
        var shard = Spawn(Shard, Transform(user).Coordinates);
        if (!_cavity.TryLodge(user, zone, shard))
            QueueDel(shard);
    }
}
