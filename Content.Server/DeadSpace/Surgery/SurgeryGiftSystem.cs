// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed partial class SurgeryGiftSystem : EntitySystem
{
    private const string PartPrefix = "part:";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryGiftComponent, OrganAddedToBodyEvent>(OnOrganIn);
        SubscribeLocalEvent<SurgeryGiftComponent, OrganRemovedFromBodyEvent>(OnOrganOut);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartIn);
        SubscribeLocalEvent<SurgeryGiftHostComponent, BodyPartRemovedEvent>(OnPartOut);
    }

    private void OnOrganIn(Entity<SurgeryGiftComponent> ent, ref OrganAddedToBodyEvent args)
    {
        Refresh(args.Body, default);
    }

    private void OnOrganOut(Entity<SurgeryGiftComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        Refresh(args.OldBody, ent.Owner);
    }

    private void OnPartIn(Entity<BodyComponent> ent, ref BodyPartAddedEvent args)
    {
        Refresh(ent.Owner, default);
    }

    private void OnPartOut(Entity<SurgeryGiftHostComponent> ent, ref BodyPartRemovedEvent args)
    {
        Refresh(ent.Owner, args.Part);
    }

    private void Refresh(EntityUid body, EntityUid skip)
    {
        if (TerminatingOrDeleted(body))
            return;

        var wanted = new ComponentRegistry();
        if (TryComp<BodyComponent>(body, out var bodyComp))
        {
            var present = new HashSet<string>();
            foreach (var (organ, _) in _body.GetBodyOrgans(body, bodyComp))
            {
                if (organ == skip)
                    continue;

                if (TryComp<SurgeryGiftComponent>(organ, out var gift))
                    Collect(wanted, gift);

                foreach (var comp in AllComps(organ))
                    present.Add(_factory.GetComponentName(comp.GetType()));
            }

            foreach (var (part, partComp) in _body.GetBodyChildren(body, bodyComp))
            {
                if (part != skip)
                    present.Add(PartPrefix + partComp.PartType);
            }

            if (TryComp<HumanoidAppearanceComponent>(body, out var look)
                && _prototype.TryIndex<SurgerySpeciesPrototype>(look.Species.Id, out var species))
            {
                foreach (var (needed, missing) in species.Lacking)
                {
                    if (present.Contains(needed))
                        continue;

                    foreach (var (name, entry) in missing)
                        wanted.TryAdd(name, entry);
                }
            }

            foreach (var (part, _) in _body.GetBodyChildren(body, bodyComp))
            {
                if (part != skip && TryComp<SurgeryGiftComponent>(part, out var gift))
                    Collect(wanted, gift);
            }
        }

        var host = EnsureComp<SurgeryGiftHostComponent>(body);
        foreach (var name in new List<string>(host.Granted))
        {
            if (wanted.ContainsKey(name) || !_factory.TryGetRegistration(name, out var registration))
                continue;

            RemComp(body, registration.Type);
            host.Granted.Remove(name);
        }

        foreach (var (name, entry) in wanted)
        {
            if (!_factory.TryGetRegistration(name, out var registration) || HasComp(body, registration.Type))
                continue;

            EntityManager.AddComponents(body, new ComponentRegistry { [name] = entry }, false);
            host.Granted.Add(name);
        }
    }

    private static void Collect(ComponentRegistry wanted, SurgeryGiftComponent gift)
    {
        foreach (var (name, entry) in gift.Grant)
            wanted.TryAdd(name, entry);
    }
}
