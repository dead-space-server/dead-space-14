// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Languages.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Content.Shared.DeadSpace.Languages.Components;
using Robust.Shared.GameStates;

namespace Content.Client.DeadSpace.Languages;

public sealed partial class LanguageSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, LanguageComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not LanguageComponentState state)
            return;

        component.KnownLanguages = state.KnownLanguages;
        component.CantSpeakLanguages = state.CantSpeakLanguages;
    }

    public void PlayEntityLexiconSound(AudioParams audioParams, EntityUid sourceUid, ProtoId<LanguagePrototype> languageId)
    {
        if (!_prototypeManager.TryIndex(languageId, out var languageProto))
            return;

        if (languageProto.LexiconSound != null)
            _audio.PlayEntity(_audio.ResolveSound(languageProto.LexiconSound), Filter.Empty(), sourceUid, false, audioParams);
    }

    public void PlayGlobalLexiconSound(AudioParams audioParams, ProtoId<LanguagePrototype> languageId)
    {
        if (!_prototypeManager.TryIndex(languageId, out var languageProto))
            return;

        if (languageProto.LexiconSound != null)
            _audio.PlayGlobal(_audio.ResolveSound(languageProto.LexiconSound), Filter.Empty(), false, audioParams);
    }
}
