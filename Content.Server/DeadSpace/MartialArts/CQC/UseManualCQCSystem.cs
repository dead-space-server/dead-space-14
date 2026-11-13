// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Actions;
using Content.Server.DeadSpace.MartialArts.Arkalyse.Components;
using Content.Server.DeadSpace.MartialArts.SmokingCarp.Components;
using Robust.Server.GameObjects;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Server.DeadSpace.MartialArts.CQC.Components;

namespace Content.Server.DeadSpace.MartialArts.CQC;

public sealed partial class UseManualCQCSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private TransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MartialArtsTrainingCQCComponent, UseInHandEvent>(OnUseManualCQC);
    }

    private void OnUseManualCQC(Entity<MartialArtsTrainingCQCComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || TryComp<ArkalyseComponent>(args.User, out _) || TryComp<SmokingCarpComponent>(args.User, out _))
            return;

        if (HasComp<CQCComponent>(args.User))
            return;

        EnsureComp<CQCComponent>(args.User);
        EnsureComp<CQCStepPunchComponent>(args.User);
        var userCQCC = EnsureComp<CQCComponent>(args.User);
        userCQCC.Params = ent.Comp.Params[0];

        _action.AddAction(args.User, ref userCQCC.CQCConcentrationActionEntity, userCQCC.CQCConcentrationAction);

        if (TryComp<MeleeWeaponComponent>(args.User, out var melee))
            melee.AttackRate = ent.Comp.AddAtackRate;

        Del(ent);
        Spawn(ent.Comp.ItemAfterLerning, _transform.GetMapCoordinates(args.User));

        args.Handled = true;
    }
}