using Content.Server.DeadSpace.MentalIllness.Components;
using Content.Shared.DeadSpace.MentalIllness;
using Content.Shared.Speech.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MentalIllness;

public sealed partial class MentalIllnessSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public void TriggerDepressionEffects(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        ApplyDepressionSpeedModifier(uid, component);
        ApplyDepressionEmoteEffect(uid, component);
        ApplyDepressionDamageModifier(uid, component);

        if (!component.Severity.TryGetValue(MentalIllnessType.Depression, out var severity))
            return;

        if (severity <= 0.05f)
            RemoveAnxiety(uid, component);
    }

    public void RemoveDepression(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.ContainsKey(MentalIllnessType.Depression))
            component.Severity.Remove(MentalIllnessType.Depression);

        component.DamageMultiply = 1.0f;
        component.MovementSpeedModifier = 1.0f;

        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void ApplyDepressionDamageModifier(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.TryGetValue(MentalIllnessType.Depression, out var severity))
            component.DepressionDamageMultiply = Math.Clamp(severity, 0.5f, 1f);
    }

    private void ApplyDepressionSpeedModifier(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        // Установить модификатор скорости на основе тяжести депрессии
        if (component.Severity.TryGetValue(MentalIllnessType.Depression, out var severity))
        {
            // Рассчитать модификатор: от 0.5 (максимальная депрессия) до 1 (норма)
            component.DepressionMovementSpeedModifier = Math.Max(0.5f, 1.0f - severity);
        }
        else
        {
            // Если тяжесть не задана, установить стандартное значение
            component.DepressionMovementSpeedModifier = 1.0f;
        }

        RefreshTotalSpeed(uid, component);
    }

    private void ApplyDepressionEmoteEffect(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!component.Severity.TryGetValue(MentalIllnessType.Depression, out var severity))
            return;

        // Определяем степень тяжести
        string severityLevel;
        if (severity < 0.3f)
            severityLevel = "low";
        else if (severity < 0.7f)
            severityLevel = "medium";
        else
            severityLevel = "high";

        var message = Loc.GetString("depression-message-random", ("severity", severityLevel));

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, true);

        if (!TryComp<VocalComponent>(uid, out var vocal) || severityLevel != "high")
            return;

        if (vocal.EmoteSounds is not { } sounds)
            return;

        _chat.TryPlayEmoteSound(uid, _proto.Index(sounds), "Crying");
    }

}
