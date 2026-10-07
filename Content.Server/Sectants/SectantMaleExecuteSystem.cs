using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;         // DamageableSystem
using Content.Server.Popups;                 // PopupSystem
using Content.Shared.Sectants;
using Robust.Shared.Prototypes;              // IPrototypeManager

namespace Content.Server.Sectants;

public sealed class SectantMaleExecuteSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SectantSystem _sectant = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantMaleExecuteActionEvent>(OnExecute);
    }

    private void OnExecute(SectantMaleExecuteActionEvent args)
    {
        var target = args.Target;
        if (!_sectant.IsDown(target))
        {
            _popup.PopupEntity(
                Loc.GetString("sectant-male-execute-target-not-down"),
                args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        var dmg = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Cellular"), 30);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: true);
        _popup.PopupEntity(
            Loc.GetString("sectant-male-execute-target-down", ("target", target)),
            args.Performer, args.Performer);

        Spawn("SectantExecuteEffect", Transform(target).Coordinates);
        args.Handled = true;
    }
}
