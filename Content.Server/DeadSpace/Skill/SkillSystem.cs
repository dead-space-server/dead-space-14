// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Skills;
using Content.Shared.DeadSpace.Skills.Prototypes;
using Content.Shared.DeadSpace.Skills.Components;
using Content.Server.Popups;
using System.Linq;
using Content.Shared.Polymorph;
using Content.Shared.Cloning.Events;

namespace Content.Server.DeadSpace.Skill;

public sealed class SkillSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("SkillSystem");

        SubscribeLocalEvent<SkillComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<SkillComponent, CloningEvent>(OnCloning);
    }

    private void OnCloning(Entity<SkillComponent> ent, ref CloningEvent args)
    {
        var skill = EnsureComp<SkillComponent>(args.CloneUid);
        skill.Skills = new Dictionary<ProtoId<SkillPrototype>, float>(ent.Comp.Skills);
    }

    private void OnPolymorphed(EntityUid uid, SkillComponent component, PolymorphedEvent args)
    {
        var skill = EnsureComp<SkillComponent>(args.NewEntity);
        skill.Skills = new Dictionary<ProtoId<SkillPrototype>, float>(component.Skills);
    }

    private void OnInit(Entity<SkillComponent> entity, ref ComponentInit args)
    {
        if (!_prototypeManager.TryIndex(entity.Comp.Group, out var group))
            return;

        foreach (var skill in group.Skills)
        {
            if (!entity.Comp.Skills.ContainsKey(skill))
                entity.Comp.Skills[skill] = 1f;
        }
    }

    public SkillInfo? GetSkillInfo(EntityUid uid, string prototypeId, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return null;

        if (!_prototypeManager.TryIndex<SkillPrototype>(prototypeId, out var skillPrototype) || skillPrototype == null)
        {
            _sawmill.Warning($"Прототип навыка {prototypeId} не найден");
            return null;
        }

        if (!component.Skills.TryGetValue(prototypeId, out var progress))
        {
            _sawmill.Warning($"Не удалось получить прогресс изучения навыка");
            return null;
        }

        SkillInfo skill = new SkillInfo(
            skillPrototype.Name,
            skillPrototype.Description,
            skillPrototype.Icon,
            progress,
            skillPrototype.IconSize
        );

        return skill;
    }

    public bool CnowThisSkill(EntityUid uid, ProtoId<SkillPrototype> prototypeId, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.Skills.TryGetValue(prototypeId, out var progress) && progress >= 1f;
    }

    public float GetSkillProgress(EntityUid uid, ProtoId<SkillPrototype> prototypeId, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return 0f;

        if (!component.Skills.TryGetValue(prototypeId, out var progress))
            return 0f;

        return progress;
    }

    public bool CanLearn(EntityUid uid, ProtoId<SkillPrototype> prototypeId, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!_prototypeManager.TryIndex(prototypeId, out var prototype))
        {
            _sawmill.Warning($"Прототип навыка {prototypeId} не найден");
            return false;
        }

        if (prototype.RequiredSkills != null
            && prototype.RequiredSkills.Count > 0
            && !CheckRequiredSkills(uid, prototype.RequiredSkills)
            )
            return false;

        return !CnowThisSkill(uid, prototypeId, component);
    }

    public void AddSkillProgress(EntityUid uid, ProtoId<SkillPrototype> prototypeId, float progress, SkillComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!_prototypeManager.TryIndex(prototypeId, out var prototype))
        {
            _sawmill.Warning($"Прототип навыка {prototypeId} не найден");
            return;
        }

        if (component.Skills.TryGetValue(prototypeId, out var currentProgress))
            component.Skills[prototypeId] = currentProgress + progress;
        else
            component.Skills[prototypeId] = progress;

        component.Skills[prototypeId] = Math.Min(1f, component.Skills[prototypeId]);

        if (component.Skills[prototypeId] > 1f - 0.0001f)
            component.Skills[prototypeId] = 1f;
    }

    public bool CheckRequiredSkills(EntityUid user, List<ProtoId<SkillPrototype>> neededSkills)
    {
        if (!TryComp<SkillComponent>(user, out var skillComponent))
            return true;

        var missingSkills = new List<string>();

        foreach (var skill in neededSkills)
        {
            if (!_prototypeManager.TryIndex(skill, out var skillPrototype) || skillPrototype == null)
            {
                _sawmill.Warning($"Прототип навыка {skill} не найден");
                continue;
            }

            if (!CnowThisSkill(user, skill, skillComponent))
                missingSkills.Add(skillPrototype.Name);
        }

        if (missingSkills.Count > 0)
        {
            var skillsText = string.Join(", ", missingSkills.Select(s => Loc.GetString($"{s}")));
            var message = Loc.GetString("skill-need-skill-message", ("skills", skillsText));

            _popup.PopupEntity(message, user, user);
            return false;
        }

        return true;
    }

}
