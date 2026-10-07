using Content.Shared.Sectants;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Sectants;

public sealed class ClientSectantHallucinationOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantHallucinationAuraComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SectantHallucinationAuraComponent comp, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;
        _sprite.SetColor((uid, sprite), Color.FromHex("#8899ff").WithAlpha(0.7f));
    }
}
