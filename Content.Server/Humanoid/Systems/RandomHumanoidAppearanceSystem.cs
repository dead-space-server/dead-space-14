using Content.Shared.Body;
using Content.Server.CharacterAppearance.Components;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

namespace Content.Server.Humanoid.Systems;

public sealed partial class RandomHumanoidAppearanceSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
||||||| base
    [Dependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
=======
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
>>>>>>> theirs

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomHumanoidAppearanceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, RandomHumanoidAppearanceComponent component, MapInitEvent args)
    {
        // If we have an initial profile/base layer set, do not randomize this humanoid.
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid) || !string.IsNullOrEmpty(humanoid.Initial))
        {
            return;
        }

        var profile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);
        //If we have a specified hair style, change it to this
        if(component.Hair != null)
            profile = profile.WithCharacterAppearance(profile.Appearance.WithHairStyleName(component.Hair));

        _humanoid.LoadProfile(uid, profile, humanoid);

        if (component.RandomizeName)
            _metaData.SetEntityName(uid, profile.Name);
    }
}
