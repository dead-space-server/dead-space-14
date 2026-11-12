using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid; // DS14
using Content.Server.Preferences.Managers;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

public sealed partial class AntagLoadProfileRuleSystem : GameRuleSystem<AntagLoadProfileRuleComponent>
{
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!; // DS14: no Nubody, profiles are applied via humanoid appearance.
    [Dependency] private IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagLoadProfileRuleComponent, AntagSelectEntityEvent>(OnSelectEntity);
    }

    private void OnSelectEntity(Entity<AntagLoadProfileRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled)
            return;

        var profile = args.Session != null
            ? _prefs.GetPreferences(args.Session.UserId).SelectedCharacter as HumanoidCharacterProfile
            : HumanoidCharacterProfile.RandomWithSpecies();


        if (profile?.Species is not { } speciesId || !ProtoMan.Resolve(speciesId, out var species))
        {
            species = ProtoMan.Index<SpeciesPrototype>(SharedHumanoidAppearanceSystem.DefaultSpecies); // DS14
        }

        if (ent.Comp.SpeciesOverride != null
            && (ent.Comp.SpeciesOverrideBlacklist?.Contains(new ProtoId<SpeciesPrototype>(species.ID)) ?? false))
        {
            species = ProtoMan.Index(ent.Comp.SpeciesOverride.Value);
        }

        args.Entity = Spawn(species.Prototype, args.Coords);
        _humanoid.LoadProfile(args.Entity.Value, profile?.WithSpecies(species.ID)); // DS14
    }
}
