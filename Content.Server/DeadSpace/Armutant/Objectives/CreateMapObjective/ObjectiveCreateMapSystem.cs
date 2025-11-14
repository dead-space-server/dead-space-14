using System.Numerics;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Content.Shared.DeadSpace.Armutant;
using System.Diagnostics.CodeAnalysis;
using Robust.Server.Audio;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Armutant.Objectives;

public sealed class ObjectiveCreateMapSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("ObjectiveCreateMapSystem");
        SubscribeLocalEvent<ObjectiveCreateMapComponent, UseInHandEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<ObjectiveCreateMapComponent> ent, ref UseInHandEvent args)
    {
        var user = args.User;

        if (ent.Comp.UsingItem)
            return;

        if (!_entities.HasComponent<ArmutantComponent>(user))
            return;

        if (!TryAddMap(ent.Comp.MapPath, out var mapGrid))
            return;

        if (!_entities.EntityExists(mapGrid))
            return;

        if (!_entities.TryGetComponent(user, out TransformComponent? userTransform))
            return;

        userTransform.Coordinates = new EntityCoordinates(mapGrid!.Value, Vector2.Zero);

        if (!string.IsNullOrEmpty(ent.Comp.SelfEffect))
        {
            SpawnAttachedTo(ent.Comp.SelfEffect, Transform(user).Coordinates);
        }

        var ambientAudio = _audio.PlayPvs(ent.Comp.Sound, mapGrid.Value);

        var resolveAudio = _audio.ResolveSound(ent.Comp.Sound);

        PlayMapAmbientAudio(resolveAudio, mapGrid.Value);

        ent.Comp.UsingItem = true;

        args.Handled = true;
    }

    private void PlayMapAmbientAudio(ResolvedSoundSpecifier soundPath, EntityUid mapUid)
    {
        var ambientAudio = _audio.PlayPvs(soundPath, mapUid);
        _audio.SetMapAudio(ambientAudio);

        var length = _audio.GetAudioLength(soundPath);

        if (length > TimeSpan.Zero)
        {
            Timer.Spawn(length, () =>
            {
                if (_entities.EntityExists(mapUid))
                {
                    PlayMapAmbientAudio(soundPath, mapUid);
                }
            });
        }
    }

    private bool TryAddMap(ResPath mapPath, [NotNullWhen(true)] out EntityUid? mapGrid)
    {
        mapGrid = null;

        var opts = DeserializationOptions.Default with { InitializeMaps = true };

        if (!_loader.TryLoadMap(mapPath, out var mapEntity, out var gridset, opts))
        {
            _sawmill.Error($"Unable to spawn map {mapPath}");
            return false;
        }

        var mapComp = _entities.GetComponent<MapComponent>(mapEntity.Value);

        mapGrid = _mapManager.GetMapEntityId(mapComp.MapId);

        return true;
    }
}
