// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.PostShaders;

/// <summary>
/// Since engine v295 the renderer reads <c>GetScreenTexture</c> / <c>RaiseShaderEvent</c> from each post-shader entry,
/// not from the sprite, so setting the old sprite flags next to <c>sprite.PostShader</c> no longer does anything.
/// This keeps the old "one post-shader per sprite" behaviour (the legacy <c>PostShader</c> getter still sees it)
/// while putting the flags where the renderer looks.
/// </summary>
public static class SinglePostShaderExtensions
{
    private const string Id = "ds14-single";

#pragma warning disable CS0618 // the legacy flags are still mirrored so callers can save and restore them
    public static void SetSinglePostShader(this SpriteSystem system, SpriteComponent sprite, ShaderInstance? shader,
        bool getScreenTexture = false, bool raiseShaderEvent = false)
    {
        system.ClearPostShaders(sprite);
        sprite.GetScreenTexture = shader != null && getScreenTexture;
        sprite.RaiseShaderEvent = shader != null && raiseShaderEvent;

        if (shader == null)
            return;

        system.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(Id, shader)
        {
            GetScreenTexture = getScreenTexture,
            RaiseShaderEvent = raiseShaderEvent,
        });
    }
#pragma warning restore CS0618
}
