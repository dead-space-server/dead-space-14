using Content.Server.DeadSpace.MentalIllness.Components;
using Content.Shared.DeadSpace.MentalIllness;
using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Necromorphs.Sanity;

namespace Content.Server.DeadSpace.MentalIllness;

public sealed partial class MentalIllnessSystem : EntitySystem
{
    public void TriggerManiaEffects(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        ApplyManiaSanityEffect(uid, component);
        ApplyManiaEmoteEffect(uid, component);
        ApplyManiaDamageModifier(uid, component);
        ApplyManiaSpeedModifier(uid, component);

        if (!component.Severity.TryGetValue(MentalIllnessType.Mania, out var severity))
            return;

        if (severity <= 0.05f)
            RemoveAnxiety(uid, component);
    }

    public void RemoveMania(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.ContainsKey(MentalIllnessType.Mania))
            component.Severity.Remove(MentalIllnessType.Mania);

        component.ManiaDamageMultiply = 1.0f;
        component.ManiaMovementSpeedModifier = 1.0f;
    }

    private void ApplyManiaDamageModifier(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.TryGetValue(MentalIllnessType.Mania, out var severity))
            component.ManiaDamageMultiply = Math.Clamp(1 / severity, 1f, 2f);
    }

    private void ApplyManiaSpeedModifier(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        // Установить модификатор скорости на основе тяжести депрессии
        if (component.Severity.TryGetValue(MentalIllnessType.Depression, out var severity))
        {
            // Рассчитать модификатор: от 0.5 (максимальная депрессия) до 1 (норма)
            component.ManiaMovementSpeedModifier = Math.Min(1.5f, 1.0f + severity);
        }
        else
        {
            // Если тяжесть не задана, установить стандартное значение
            component.ManiaMovementSpeedModifier = 1.0f;
        }

        RefreshTotalSpeed(uid, component);
    }

    private void ApplyManiaEmoteEffect(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        // Получаем степень мании
        if (!component.Severity.TryGetValue(MentalIllnessType.Mania, out var maniaSeverity))
            return;

        // Определяем степень тяжести
        string severityLevel;
        if (maniaSeverity < 0.3f)
            severityLevel = "low";
        else if (maniaSeverity < 0.7f)
            severityLevel = "medium";
        else
            severityLevel = "high";

        // Случайный выбор эмоции (angry или sad)
        var emotionType = _random.NextDouble() < 0.5 ? "angry" : "sad"; // 50% шанс на выбор эмоции

        // Формируем сообщение в зависимости от выбранной эмоции
        string message = emotionType switch
        {
            "angry" => Loc.GetString("mania-message-angry", ("severity", severityLevel)),
            "sad" => Loc.GetString("mania-message-sad", ("severity", severityLevel)),
            _ => ""
        };

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, true);
    }

    private void ApplyManiaSanityEffect(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!component.Severity.TryGetValue(MentalIllnessType.Mania, out var maniaSeverity))
            return;

        if (!TryComp<SanityComponent>(uid, out var sanity))
            return;

        if (_random.NextDouble() < 0.3)
        {
            float sanityChange = 0f;
            if (maniaSeverity < 0.3f)
                sanityChange = 10f;
            else if (maniaSeverity < 0.7f)
                sanityChange = 25f;
            else
                sanityChange = 50f;

            var newSanity = Math.Clamp(sanityChange, -100f, 100f);

            _sanity.TryAddSanityLvl(uid, -newSanity, sanity);
        }

    }
}
