// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.TheCircle.NecroOrgans;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.TheCircle.NecroOrgans;

public sealed partial class NecroOrganRegrowthSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroOrganRegrowthComponent, LimbOrganRuptureChanceEvent>(OnRuptureChance);
        SubscribeLocalEvent<NecroOrganRegrowthComponent, LimbOrgansRupturedEvent>(OnRuptured);
    }

    private void OnRuptureChance(Entity<NecroOrganRegrowthComponent> ent, ref LimbOrganRuptureChanceEvent args)
    {
        if (ent.Comp.Necro == LimbOrganMask.None)
            return;

        args.Boosted |= ent.Comp.Necro;
        args.Multiplier = ent.Comp.NecroRuptureMultiplier;
    }

    private void OnRuptured(Entity<NecroOrganRegrowthComponent> ent, ref LimbOrgansRupturedEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        for (var i = 0; i < LimbOrganRules.Count; i++)
        {
            var bit = (LimbOrganMask) (1 << i);
            if ((args.Ruptured & bit) == 0 || !Ready(ent.Comp, (LimbOrgan) i))
                continue;

            args.Regrow |= bit;
            Regrown(ent, (LimbOrgan) i);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Organs that burst while on cooldown grow back once it runs out.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NecroOrganRegrowthComponent, LimbBodyComponent>();
        while (query.MoveNext(out var uid, out var regrowth, out var limbs))
        {
            if (now < regrowth.NextCheck)
                continue;

            regrowth.NextCheck = now + CheckInterval;
            var ruptured = _limbs.GetRupturedOrgans(uid, limbs);
            if (ruptured == LimbOrganMask.None || _mobState.IsDead(uid))
                continue;

            for (var i = 0; i < LimbOrganRules.Count; i++)
            {
                var organ = (LimbOrgan) i;
                if ((ruptured & (LimbOrganMask) (1 << i)) == 0 || !Ready(regrowth, organ))
                    continue;

                if (_limbs.RegrowOrgan(uid, organ, limbs))
                    Regrown((uid, regrowth), organ);
            }
        }
    }

    private bool Ready(NecroOrganRegrowthComponent regrowth, LimbOrgan organ)
    {
        return !regrowth.NextRegrow.TryGetValue(organ, out var next) || _timing.CurTime >= next;
    }

    private void Regrown(Entity<NecroOrganRegrowthComponent> ent, LimbOrgan organ)
    {
        ent.Comp.Necro |= (LimbOrganMask) (1 << (int) organ);
        ent.Comp.NextRegrow[organ] = _timing.CurTime + ent.Comp.Cooldown;
        _popup.PopupEntity(
            Loc.GetString("necro-organ-regrowth-popup", ("organ", Loc.GetString($"health-analyzer-organ-{(int) organ}"))),
            ent,
            ent,
            PopupType.MediumCaution);
    }
}
