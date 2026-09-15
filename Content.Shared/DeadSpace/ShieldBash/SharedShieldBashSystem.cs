// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ShieldBash.Components;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.DeadSpace.ShieldBash;

/// <summary>
/// Handles banging a <see cref="ShieldBashToolComponent"/> weapon (baton,
/// truncheon, energy machete, ...) against a <see cref="ShieldBashComponent"/>
/// shield — a taunt/flavor interaction, works whether the shield is held in
/// your other hand or just lying in the world. Rate-limited per shield via
/// a dedicated <see cref="UseDelayComponent"/> ID so it can't be spammed,
/// and independent of whatever else that shield's UseDelay is used for
/// (e.g. an energy shield's fold/unfold cooldown).
/// </summary>
public sealed class SharedShieldBashSystem : EntitySystem
{
    private const string DelayId = "shieldbash";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShieldBashComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<ShieldBashComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<ShieldBashToolComponent>(args.Used))
            return;

        _useDelay.SetLength(ent.Owner, ent.Comp.Delay, DelayId);
        if (!TryComp<UseDelayComponent>(ent, out var useDelay) ||
            !_useDelay.TryResetDelay((ent.Owner, useDelay), true, DelayId))
        {
            // Still on cooldown from the last bash — swallow the click.
            args.Handled = true;
            return;
        }

        _audio.PlayPredicted(ent.Comp.Sound, ent, args.User);

        args.Handled = true;
    }
}
