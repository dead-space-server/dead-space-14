// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Holopad;
using Content.Shared.Speech;
using Content.Shared.Telephone;
using Content.Shared.Verbs;
using Content.Server.Popups;
using Content.Server.Telephone;
using Content.Server.Holopad; 
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.PortableHolopad;

public sealed partial class PortableHolopadSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private TransformSystem _xformSystem = default!;
    [Dependency] private TelephoneSystem _telephoneSystem = default!;
    [Dependency] private HolopadSystem _holopadSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<PortableHolopadComponent, GetVerbsEvent<AlternativeVerb>>(AddDeployVerb);
        SubscribeLocalEvent<PortableHolopadComponent, TelephoneMessageSentEvent>(OnTelephoneMessageSent);
        SubscribeLocalEvent<PortableHolopadComponent, TelephoneCallCommencedEvent>(OnCallEvent);
        SubscribeLocalEvent<PortableHolopadComponent, TelephoneStateChangeEvent>(OnStateEvent);
    }

    private void OnCallEvent(Entity<PortableHolopadComponent> entity, ref TelephoneCallCommencedEvent args) 
        => CheckAndSuppress(entity);

    private void OnStateEvent(Entity<PortableHolopadComponent> entity, ref TelephoneStateChangeEvent args) 
        => CheckAndSuppress(entity);

    private void CheckAndSuppress(Entity<PortableHolopadComponent> entity)
    {
        if (entity.Comp.Deployed)
            return;

        ResetSpeakerToDevice(entity);
        Timer.Spawn(10, () => EnsureNoHologram(entity));
    }

    private void EnsureNoHologram(Entity<PortableHolopadComponent> entity)
    {
        if (Deleted(entity) || entity.Comp.Deployed)
            return;

        if (TryComp<HolopadComponent>(entity, out var holopad) && holopad.Hologram != null)
        {
            _holopadSystem.DeleteHologram(holopad.Hologram.Value, (entity, holopad));
            ResetSpeakerToDevice(entity);
        }
    }

    private void ResetSpeakerToDevice(Entity<PortableHolopadComponent> entity)
    {
        if (!TryComp<SpeechComponent>(entity, out var speech) ||
            !TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        _telephoneSystem.SetSpeakerForTelephone((entity, telephone), (entity, speech));
    }

    public void ToggleDeployed(Entity<PortableHolopadComponent> entity, EntityUid user)
    {
        if (_container.IsEntityInContainer(entity))
        {
            _popupSystem.PopupEntity("Голопад должен быть на полу!", entity, user);
            return;
        }

        if (!TryComp<HolopadComponent>(entity, out var holopad) || 
            !TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        entity.Comp.Deployed = !entity.Comp.Deployed;

        if (entity.Comp.Deployed)
        {
            if (_telephoneSystem.IsTelephoneEngaged((entity, telephone)))
                _holopadSystem.GenerateHologram((entity, holopad));

            _xformSystem.AnchorEntity(entity);
            _popupSystem.PopupEntity("Голопад развернут.", entity, user);
        }
        else
        {
            _xformSystem.Unanchor(entity);
            EnsureNoHologram(entity);

            ResetSpeakerToDevice(entity);

            _popupSystem.PopupEntity("Голопад собран.", entity, user);
        }
        Dirty(entity);
    }

    private void AddDeployVerb(Entity<PortableHolopadComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract) return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb {
            Act = () => ToggleDeployed(entity, user),
            Text = entity.Comp.Deployed ? "Собрать голопад." : "Развернуть голопад.",
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png"))
        });
    }

    private void OnTelephoneMessageSent(Entity<PortableHolopadComponent> entity, ref TelephoneMessageSentEvent args)
    {
        if (!entity.Comp.Deployed)
            ResetSpeakerToDevice(entity);
    }
}
