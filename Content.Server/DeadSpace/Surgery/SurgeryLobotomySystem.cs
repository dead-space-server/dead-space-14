// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryLobotomySystem : EntitySystem
{
    private const string Effect = "Lobotomy";

    [Dependency] private readonly SharedCombatModeSystem _combat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, SurgeryEffectEvent>(OnEffect);
    }

    private void OnEffect(Entity<LimbBodyComponent> ent, ref SurgeryEffectEvent args)
    {
        if (args.Handled || args.Effect != Effect || HasComp<SurgeryLobotomyComponent>(ent))
            return;

        args.Handled = true;
        EnsureComp<SurgeryLobotomyComponent>(ent);
        _combat.SetInCombatMode(ent, false);
    }
}
