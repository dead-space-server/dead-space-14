// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Sectants;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Sectants;

public sealed class ClientSectantAuraOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SectantAuraOverlayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectantAuraOverlayComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SectantAuraOverlayComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var shader = _protoMan.Index<ShaderPrototype>(ent.Comp.Shader).InstanceUnique();
        shader.SetParameter("auraColor", ent.Comp.Color);
        shader.SetParameter("pulseSpeed", ent.Comp.PulseSpeed);
        shader.SetParameter("outlineWidth", ent.Comp.OutlineWidth);
        shader.SetParameter("spikeCount", ent.Comp.SpikeCount);

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetShader(i, shader);
        }
    }

    private void OnShutdown(Entity<SectantAuraOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Сбрасываем шейдер на дефолтный.
        // Если вы точно знаете, что на слоях был "unshaded" или другой шейдер,
        // можно вернуть его строкой: sprite.LayerSetShader(i, "unshaded");
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetShader(i, null, null);
        }
    }
}
