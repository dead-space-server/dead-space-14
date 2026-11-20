// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.MartialArts.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Server.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Server.DeadSpace.MartialArts.Arkalyse.Components;
using Content.Server.DeadSpace.MartialArts.CQC.Components;
using Robust.Server.GameObjects;

namespace Content.Server.DeadSpace.MartialArts;

public sealed partial class UseArkalyseBookSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MartialArtsTrainingArkalyseComponent, UseInHandEvent>(OnUseBookArkalyse);
    }
    private void OnUseBookArkalyse(Entity<MartialArtsTrainingArkalyseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled ||
            HasComp<SmokingCarpComponent>(args.User) ||
            HasComp<CQCComponent>(args.User))
            return;

        if (TryComp<ArkalyseComponent>(args.User, out var existing))
        {
            if (!existing.LearnedFromManual)
                _popup.PopupEntity(Loc.GetString("arkalyse-remove-gloves-before-learn"), args.User, args.User);

            args.Handled = true;
            return;
        }

        var userArkalyse = EnsureComp<ArkalyseComponent>(args.User);
        userArkalyse.LearnedFromManual = true;
        userArkalyse.Params = ent.Comp.Params[0];

        foreach (var actionId in userArkalyse.BaseArkalyse)
            _action.AddAction(args.User, actionId);

        if (TryComp<MeleeWeaponComponent>(args.User, out var melee))
            melee.AttackRate = ent.Comp.AddAtackRate;

        Del(ent);
        Spawn(ent.Comp.ItemAfterLerning, _transform.GetMapCoordinates(args.User));

        args.Handled = true;
    }
}
