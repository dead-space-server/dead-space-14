// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Sound.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client.DeadSpace.Audio;

public sealed partial class ItemSoundAudioSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly HashSet<EntityUid> _pendingApply = new();
    private float _itemSoundsVolume = 1f;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(Robust.Client.Audio.AudioSystem));

        SubscribeLocalEvent<ItemSoundAudioComponent, ComponentStartup>(OnItemSoundStartup);
        SubscribeLocalEvent<ItemSoundAudioComponent, AfterAutoHandleStateEvent>(OnItemSoundAfterState);
        Subs.CVar(_cfg, CCCCVars.ItemSoundsVolume, SetItemSoundsVolume, true);
    }

    private void OnItemSoundStartup(EntityUid uid, ItemSoundAudioComponent component, ComponentStartup args)
    {
        ApplyItemSoundVolume(uid, component);
        _pendingApply.Add(uid);
    }

    private void OnItemSoundAfterState(Entity<ItemSoundAudioComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyItemSoundVolume(ent.Owner, ent.Comp);
        _pendingApply.Add(ent.Owner);
    }

    private void SetItemSoundsVolume(float volume)
    {
        _itemSoundsVolume = volume;
        ApplyItemSoundVolumes();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_pendingApply.Count == 0)
            return;

        foreach (var uid in _pendingApply)
        {
            if (!TryComp<ItemSoundAudioComponent>(uid, out var itemSound))
                continue;

            ApplyItemSoundVolume(uid, itemSound);
        }

        _pendingApply.Clear();
    }

    private void ApplyItemSoundVolumes()
    {
        var query = EntityQueryEnumerator<ItemSoundAudioComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var itemSound, out var audio))
        {
            ApplyItemSoundVolume(uid, itemSound, audio);
        }
    }

    private void ApplyItemSoundVolume(EntityUid uid, ItemSoundAudioComponent itemSound, AudioComponent audio)
    {
        var volume = itemSound.BaseVolume + SharedAudioSystem.GainToVolume(_itemSoundsVolume);
        _audio.SetVolume(uid, volume, audio);
    }

    private void ApplyItemSoundVolume(EntityUid uid, ItemSoundAudioComponent itemSound)
    {
        if (!TryComp<AudioComponent>(uid, out var audio))
            return;

        ApplyItemSoundVolume(uid, itemSound, audio);
    }
}
