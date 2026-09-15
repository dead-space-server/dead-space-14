// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Gavel.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.DeadSpace.Gavel;

/// <summary>
/// Handles striking a <see cref="GavelBlockComponent"/> with a
/// <see cref="GavelHammerComponent"/> item: plays a pitch-varied hit sound
/// and shows a strike popup to everyone nearby, rate-limited per block by
/// its <see cref="UseDelayComponent"/> so it can't be spammed.
/// </summary>
public sealed class SharedGavelSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GavelBlockComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<GavelBlockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<GavelHammerComponent>(args.Used))
            return;

        // Block is still on cooldown from a previous hit — swallow the click
        // instead of playing the sound/popup again, so it can't be spammed.
        if (TryComp<UseDelayComponent>(ent, out var useDelay) && !_useDelay.TryResetDelay((ent.Owner, useDelay), true))
        {
            args.Handled = true;
            return;
        }

        _audio.PlayPredicted(ent.Comp.Sound, ent, args.User);
        _popup.PopupPredicted(
            Loc.GetString("gavel-block-strike-self"),
            Loc.GetString("gavel-block-strike-others", ("user", args.User)),
            ent,
            args.User);

        args.Handled = true;
    }
}
