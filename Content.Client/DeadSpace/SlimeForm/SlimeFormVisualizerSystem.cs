// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Part;
using Content.Shared.DeadSpace.SlimeForm;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.DeadSpace.SlimeForm;

public sealed class SlimeFormVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeFormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SlimeFormComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<BodyPartComponent, AppearanceChangeEvent>(OnLimbAppearance);
    }

    public void SetCarriedPose(EntityUid uid, bool carried)
    {
        if (!TryComp<SlimeFormComponent>(uid, out var slime) || !slime.IsSlime)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetRsiState((uid, sprite), "body", carried ? "held" : "slime");
        _sprite.LayerSetRsiState((uid, sprite), "face", carried ? "face-held" : "face");
    }

    private void OnStartup(Entity<SlimeFormComponent> ent, ref ComponentStartup args)
    {
        if (!ent.Comp.IsSlime || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetDrawDepth((ent.Owner, sprite), (int) DrawDepth.Mobs);
        _sprite.SetColor((ent.Owner, sprite), Color.White);
    }

    private void OnAppearance(Entity<SlimeFormComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!ent.Comp.IsSlime || args.Sprite == null)
            return;

        _sprite.SetDrawDepth((ent.Owner, args.Sprite), (int) DrawDepth.Mobs);
        _sprite.SetColor((ent.Owner, args.Sprite), Color.White);

        if (args.AppearanceData.TryGetValue(SlimeFormVisuals.BodyColor, out var body) && body is Color bodyColor)
            _sprite.LayerSetColor((ent.Owner, args.Sprite), "body", bodyColor.WithAlpha(1f));

        if (args.AppearanceData.TryGetValue(SlimeFormVisuals.FaceColor, out var face) && face is Color faceColor)
            _sprite.LayerSetColor((ent.Owner, args.Sprite), "face", faceColor.WithAlpha(1f));
    }

    private void OnLimbAppearance(Entity<BodyPartComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.AppearanceData.TryGetValue(SlimeFormVisuals.BodyColor, out var body) && body is Color bodyColor)
            _sprite.SetColor((ent.Owner, args.Sprite), bodyColor.WithAlpha(1f));
    }
}
