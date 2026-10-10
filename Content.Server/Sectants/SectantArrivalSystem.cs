using Content.Shared.Sectants;
using Robust.Shared.Audio;

namespace Content.Server.Sectants;

public sealed class SectantArrivalSystem : EntitySystem
{
    [Dependency] private readonly SectantAnnouncementSystem _announce = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantDisappearComponent, ComponentStartup>(OnSpawn);
    }

    private void OnSpawn(EntityUid uid, SectantDisappearComponent comp, ComponentStartup args)
    {
        if (!HasComp<SectantInvisibilityToggleComponent>(uid) &&
            !HasComp<SectantRandomTeleportOnDamageComponent>(uid))
            return;

        _announce.Announce(
            "ОНО ПРИБЛИЖАЕТСЯ…",
            new SoundPathSpecifier("/Audio/_DeadSpace/TEMP_FOR_EVENT/little_gabry/laughAl.ogg"),
            Color.FromHex("#cc0000"),
            flashDuration: 3.0f,
            shakeIntensity: 8f);
    }
}
