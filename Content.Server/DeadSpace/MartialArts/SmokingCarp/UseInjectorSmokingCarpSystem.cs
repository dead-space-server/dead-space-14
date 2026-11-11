// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Actions;
using Content.Server.DeadSpace.MartialArts.Arkalyse.Components;
using Content.Server.DeadSpace.MartialArts.CQC.Components;
using Content.Server.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Server.Guardian;
using Robust.Server.GameObjects;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp.Components;

namespace Content.Server.DeadSpace.MartialArts.SmokingCarp;
public sealed class UseArkalyseBookSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MartialArtsTrainingCarpComponent, UseInHandEvent>(OnUseInjectorSmokingCarp);
    }

    private void OnUseInjectorSmokingCarp(Entity<MartialArtsTrainingCarpComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled ||
            HasComp<ArkalyseComponent>(args.User) ||
            HasComp<CQCComponent>(args.User) ||
            HasComp<GuardianHostComponent>(args.User))
            return;

        if (HasComp<SmokingCarpComponent>(args.User))
            return;

        EnsureComp<SmokingCarpTripPunchComponent>(args.User);
        EnsureComp<SmokingCarpNotShotComponent>(args.User);
        var userSmokingCarp = EnsureComp<SmokingCarpComponent>(args.User);
        userSmokingCarp.Params = ent.Comp.Params[0];

        foreach (var actionId in userSmokingCarp.BaseSmokingCarp)
            _action.AddAction(args.User, actionId);

        if (TryComp<MeleeWeaponComponent>(args.User, out var melee))
            melee.AttackRate = ent.Comp.AddAtackRate;

        Del(ent);
        Spawn(ent.Comp.ItemAfterLerning, _transform.GetMapCoordinates(args.User));

        args.Handled = true;
    }
}
