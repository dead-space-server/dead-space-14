using Content.Server.DeadSpace.MentalIllness.Components;
using Content.Shared.DeadSpace.MentalIllness;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.MentalIllness;

public sealed partial class MentalIllnessSystem : EntitySystem
{
    [Dependency] private readonly ActorSystem _actors = default!;
    const float BaseVolume = 0.5f; // Базовое значение громкости
    public void TriggerParanoiaEffects(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        SendParanoiaMessage(uid, component);
        SendParanoiaSound(uid, component);

        if (!component.Severity.TryGetValue(MentalIllnessType.Paranoia, out var severity))
            return;

        if (severity <= 0.05f)
            RemoveAnxiety(uid, component);
    }

    public void RemoveParanoia(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (component.Severity.ContainsKey(MentalIllnessType.Paranoia))
            component.Severity.Remove(MentalIllnessType.Paranoia);
    }

    public void SendParanoiaSound(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        var session = _actors.GetSession(uid);

        if (component.SoundParanoia != null)
        {
            if (session != null)
            {
                Filter playerFilter = Filter.Empty().AddPlayer(session);

                // Определяем громкость на основе степени тяжести
                if (component.Severity.TryGetValue(MentalIllnessType.Paranoia, out var severity))
                {
                    // Громкость от 0.5 (лёгкая паранойя) до 1.5 (тяжёлая паранойя)
                    var volume = BaseVolume + severity;

                    _audio.PlayGlobal(component.SoundParanoia, playerFilter, false, AudioParams.Default.WithVolume(volume));
                }
            }
        }
    }

    public void SendParanoiaMessage(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        string severity = component.Severity.TryGetValue(MentalIllnessType.Paranoia, out var level)
        ? level < 0.3f ? "low"
        : level < 0.7f ? "medium"
        : "high"
        : "default";


        var message = Loc.GetString("paranoia-message-random", ("severity", severity));

        _popup.PopupEntity(message, uid, uid, PopupType.LargeCaution);
    }

}
