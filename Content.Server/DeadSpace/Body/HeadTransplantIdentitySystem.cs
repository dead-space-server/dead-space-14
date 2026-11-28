// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;

namespace Content.Server.DeadSpace.Body;

public sealed partial class HeadTransplantIdentitySystem : EntitySystem
{
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, LimbGraftedEvent>(OnHeadGrafted);
    }

    private void OnHeadGrafted(Entity<BodyComponent> ent, ref LimbGraftedEvent args)
    {
        if (args.Zone != LimbZone.Head)
            return;

        if (!TryComp<LimbFleshComponent>(args.Part, out var flesh) || flesh.Donor.Length == 0)
            return;

        if (Name(ent) != flesh.Donor)
        {
            _metaData.SetEntityName(ent, flesh.Donor, raiseEvents: false);
            _identity.QueueIdentityUpdate(ent);
        }

        if (flesh.DonorVoices.Count == 0)
            return;

        if (TryComp<TTSComponent>(ent, out var tts))
        {
            tts.Voices = new(flesh.DonorVoices);
            Dirty(ent, tts);
        }

        if (TryComp<HumanoidAppearanceComponent>(ent, out var look))
        {
            look.TtsVoices = new(flesh.DonorVoices);
            Dirty(ent, look);
        }
    }
}
