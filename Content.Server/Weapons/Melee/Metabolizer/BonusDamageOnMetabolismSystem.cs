using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Body.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee.Metabolizer;
using Robust.Shared.Prototypes;

namespace Content.Server.Weapons.Melee.Metabolizer;

public sealed class BonusDamageOnMetabolismSystem : EntitySystem // DS14 - metabolism is server-side on this baseline
{
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!; // DS14 - pre-v288 IoC
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // DS14 - current engine baseline has no EntitySystem.ProtoMan shortcut.

    // DS14-Start - pre-v288 explicit event subscriptions
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BonusDamageOnMetabolismComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<BonusDamageOnMetabolismComponent, MeleeHitEvent>(OnSwingTrigger);
    }
    // DS14-End

    private void OnGetVerb(Entity<BonusDamageOnMetabolismComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var allMetabolizers = _prototypeManager.EnumeratePrototypes<MetabolizerTypePrototype>().ToList().OrderBy(x => Loc.GetString(x.LocalizedName));

        byte index = 0;
        foreach (var metabolizer in allMetabolizers)
        {
            if (ent.Comp.ExcludedMetabolizers.Contains(metabolizer))
                continue;

            var currIndex = index;
            var verb = new Verb
            {
                Priority = currIndex,
                Category = VerbCategory.Metabolizers,
                Disabled = ent.Comp.SelectedMetabolizer == metabolizer,
                Act = () =>
                {
                    ent.Comp.SelectedMetabolizer = metabolizer;
                    Dirty(ent);
                },
                Text = Loc.GetString(metabolizer.LocalizedName),
            };
            args.Verbs.Add(verb);
            index++;
        }
    }

    private void OnSwingTrigger(Entity<BonusDamageOnMetabolismComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.SelectedMetabolizer is null)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            if (TryComp<MobStateComponent>(hitEntity, out var mobState) && !ent.Comp.ValidMobStates.Contains(mobState.CurrentState))
                continue;

            if (!_metabolizer.BodyHasMetabolizer(hitEntity, ent.Comp.SelectedMetabolizer.Value))
                continue;

            // Add the bonus damage and quit!
            args.BonusDamage += ent.Comp.Damage; // DS14 - fix upstream =+ typo
            return;
        }
    }
}
