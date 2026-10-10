using Content.Shared.Sectants;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Sectants;

/// <summary>Smoothly interpolates stealth visibility for sectants.</summary>
public sealed class ClientSectantFadeSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    private readonly Dictionary<EntityUid, float> _lastAlpha = new();

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<StealthComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var stealth, out var sprite))
        {
            var target = _stealth.GetVisibility(uid, stealth);

            _lastAlpha.TryGetValue(uid, out var cur);
            var next = MathHelper.Lerp(cur, target, frameTime * 8f);
            _lastAlpha[uid] = next;
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(next));
        }
    }
}
