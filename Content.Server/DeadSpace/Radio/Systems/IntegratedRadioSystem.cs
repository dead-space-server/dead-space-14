// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Radio.Components;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Radio.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Radio.Systems;

/// <summary>Puts a radio inside the creature and gives it the radio's on/off action.</summary>
public sealed class IntegratedRadioSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntegratedRadioComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IntegratedRadioComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<IntegratedRadioComponent> ent, ref MapInitEvent args)
    {
        var container = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        var radio = Spawn(ent.Comp.Radio, Transform(ent).Coordinates);
        if (!_containers.Insert(radio, container))
        {
            QueueDel(radio);
            return;
        }

        ent.Comp.RadioEntity = radio;

        // The radio's own on/off action, put on the creature's bar.
        if (TryComp<RadioToggleActionComponent>(radio, out var toggle) && toggle.ActionEntity is { } action)
            _actions.GrantContainedAction(ent.Owner, radio, action);
    }

    private void OnShutdown(Entity<IntegratedRadioComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.RadioEntity is { } radio && !TerminatingOrDeleted(radio))
            QueueDel(radio);
    }
}
