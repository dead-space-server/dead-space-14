// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Core;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.DeadSpace.Husbandry.Sex;

/// <summary>
/// Picks the sex of animals when they are born. The server rolls it, the clients get it with the component.
/// </summary>
public sealed class AnimalSexSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalSexComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<AnimalSexComponent> ent, ref MapInitEvent args)
    {
        EnsureRolled(ent);
    }

    /// <summary>
    /// Rolls the sex if it was not rolled yet. Safe to call many times, other systems call it when they need the
    /// sex during map init, where the order of components is not defined.
    /// </summary>
    public void EnsureRolled(Entity<AnimalSexComponent> ent)
    {
        if (_net.IsClient || !ent.Comp.Randomize)
            return;

        ent.Comp.Randomize = false;
        ent.Comp.Sex = GrowthRules.RollSex(_random.NextDouble(), ent.Comp.MaleChance);
        Dirty(ent);
    }

    /// <summary>
    /// Sets the sex by hand (admins, tests): it is not rolled afterwards.
    /// </summary>
    public void SetSex(Entity<AnimalSexComponent> ent, AnimalSexKind sex)
    {
        ent.Comp.Randomize = false;
        ent.Comp.Sex = sex;
        Dirty(ent);
    }
}
