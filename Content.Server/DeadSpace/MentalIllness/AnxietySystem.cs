using Content.Server.DeadSpace.MentalIllness.Components;
using Content.Shared.DeadSpace.MentalIllness;
using Content.Shared.Popups;
using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.Hands.Components;

namespace Content.Server.DeadSpace.MentalIllness;

public sealed partial class MentalIllnessSystem : EntitySystem
{
    public void TriggerAnxietyEffects(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        ApplyAnxietyEmoteEffect(uid, component);
        ApplyAnxietyDropItem(uid, component);

        if (!component.Severity.TryGetValue(MentalIllnessType.Anxiety, out var severity))
            return;

        if (severity <= 0.05f)
            RemoveAnxiety(uid, component);
    }

    public void RemoveAnxiety(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.ContainsKey(MentalIllnessType.Anxiety))
            component.Severity.Remove(MentalIllnessType.Anxiety);
    }

    private void ApplyAnxietyDropItem(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!TryComp(uid, out HandsComponent? handsComp))
            return;

        if (!component.Severity.TryGetValue(MentalIllnessType.Anxiety, out var anxietySeverity))
            return;

        if (_random.NextDouble() < anxietySeverity)
            return;

        if (TryComp<HandsComponent>(uid, out var hands))
        {
            foreach (var hand in hands.Hands.Keys)
            {
                _hands.TryDrop((uid, hands), hand);
            }
        }

        var message = Loc.GetString("anxiety-message-drop-item");
        _sharedJittering.DoJitter(uid, TimeSpan.FromSeconds(2), true);

        _popup.PopupEntity(message, uid, uid, PopupType.LargeCaution);
    }

    private void ApplyAnxietyEmoteEffect(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        // Получаем степень мании
        if (!component.Severity.TryGetValue(MentalIllnessType.Anxiety, out var anxietySeverity))
            return;

        // Определяем степень тяжести
        string severityLevel;
        if (anxietySeverity < 0.3f)
            severityLevel = "low";
        else if (anxietySeverity < 0.7f)
            severityLevel = "medium";
        else
            severityLevel = "high";

        // Случайный выбор эмоции (angry или sad)
        var emotionType = _random.NextDouble() < 0.5 ? "paranoia" : "anxiety"; // 50% шанс на выбор эмоции

        // Формируем сообщение в зависимости от выбранной эмоции
        string message = emotionType switch
        {
            "paranoia" => Loc.GetString("paranoia-message-random", ("severity", severityLevel)),
            "anxiety" => Loc.GetString("anxiety-message", ("severity", severityLevel)),
            _ => ""
        };

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, true);
    }

}
