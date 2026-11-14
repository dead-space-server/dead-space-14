using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Shared.DeadSpace.FlipPaper;

/// <summary>
/// Верб переворота (как у игральной карты) для <see cref="FlippablePaperComponent"/>.
/// Лицевая сторона листка — та, где есть компонент Paper; вторая (чистая)
/// выглядит пустой и текст на ней посмотреть нельзя.
/// </summary>
public sealed class FlippablePaperSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlippablePaperComponent, GetVerbsEvent<Verb>>(AddFlipVerb);
    }

    private void AddFlipVerb(EntityUid uid, FlippablePaperComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || !args.CanComplexInteract)
            return;

        // Анкорнутые листки не переворачиваем.
        if (TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == BodyType.Static)
            return;

        Verb verb = new()
        {
            Act = () => Flip(uid, component),
            Text = Loc.GetString("flippable-paper-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
            Priority = -4,
            DoContactInteraction = true
        };
        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Заменяет листок на перевернутый вариант, перенося текст и штампы.
    /// </summary>
    public void Flip(EntityUid uid, FlippablePaperComponent component)
    {
        var oldTransform = Comp<TransformComponent>(uid);
        var entity = PredictedSpawnAtPosition(component.FlipTarget, oldTransform.Coordinates);
        var newTransform = Comp<TransformComponent>(entity);
        _transform.SetLocalRotation(entity, oldTransform.LocalRotation);
        _transform.Unanchor(entity, newTransform);

        // Считываем текущие данные бумаги: либо с компонента Paper,
        // либо из "чемоданчика" чистой стороны.
        var content = "";
        var signatures = new List<string>();
        var stampedBy = new List<StampDisplayInfo>();
        string? stampState = null;

        if (TryComp<PaperComponent>(uid, out var oldPaper))
        {
            content = oldPaper.Content;
            signatures = new List<string>(oldPaper.Signatures);
            stampedBy = new List<StampDisplayInfo>(oldPaper.StampedBy);
            stampState = oldPaper.StampState;
        }
        else
        {
            content = component.StashedContent;
            signatures = new List<string>(component.StashedSignatures);
            stampedBy = new List<StampDisplayInfo>(component.StashedStampedBy);
            stampState = component.StashedStampState;
        }

        if (!TryComp<FlippablePaperComponent>(entity, out var newFlip))
        {
            Log.Error($"FlippablePaper mirror prototype {MetaData(entity).EntityPrototype} has no FlippablePaperComponent!");
            PredictedDel(uid);
            return;
        }

        // Переносим "чемоданчик" с данными бумаги на новую сторону.
        newFlip.StashedContent = content;
        newFlip.StashedSignatures = signatures;
        newFlip.StashedStampedBy = stampedBy;
        newFlip.StashedStampState = stampState;
        Dirty(entity, newFlip);

        // Если новая сторона умеет читать (есть Paper) — отдаём ей текст.
        if (TryComp<PaperComponent>(entity, out var newPaper))
        {
            _paper.SetContent((entity, newPaper), content);

            newPaper.StampedBy = new List<StampDisplayInfo>(stampedBy);
            newPaper.StampState = stampState;
            newPaper.Signatures = new List<string>(signatures);
            Dirty(entity, newPaper);

            if (TryComp<AppearanceComponent>(entity, out var appearance))
                _appearance.SetData(entity, PaperVisuals.Stamp, stampState ?? "", appearance);
        }

        PredictedDel(uid);
    }
}
