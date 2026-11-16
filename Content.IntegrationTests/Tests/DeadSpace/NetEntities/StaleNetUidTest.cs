// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Content.Server.DeadSpace.NetEntities;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.NetEntities;

[TestFixture]
public sealed class StaleNetUidTest
{
    [Test]
    public async Task DeletedRefsClearOnTheNextTickAndLiveRefsStay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var owner = EntityUid.Invalid;
        var keeper = EntityUid.Invalid;
        var gone = EntityUid.Invalid;

        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            owner = ent.SpawnEntity(null, MapCoordinates.Nullspace);
            keeper = ent.SpawnEntity(null, MapCoordinates.Nullspace);
            gone = ent.SpawnEntity(null, MapCoordinates.Nullspace);

            var jukebox = ent.EnsureComponent<JukeboxComponent>(owner);
            SetField(jukebox, "AudioStream", keeper);
            ent.Dirty(owner, jukebox);

            var module = ent.EnsureComponent<ItemBorgModuleComponent>(owner);
            var stored = StoredItems(module);
            stored["keep"] = keeper;
            stored["drop"] = gone;
            ent.Dirty(owner, module);

            var trigger = ent.EnsureComponent<StepTriggerComponent>(owner);
            var colliding = Colliding(trigger);
            colliding.Add(keeper);
            colliding.Add(gone);
            ent.Dirty(owner, trigger);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            var jukebox = ent.GetComponent<JukeboxComponent>(owner);
            var stored = StoredItems(ent.GetComponent<ItemBorgModuleComponent>(owner));
            var colliding = Colliding(ent.GetComponent<StepTriggerComponent>(owner));
            Assert.Multiple(() =>
            {
                Assert.That(jukebox.AudioStream, Is.EqualTo(keeper));
                Assert.That(stored["keep"], Is.EqualTo(keeper));
                Assert.That(stored["drop"], Is.EqualTo(gone));
                Assert.That(colliding, Does.Contain(keeper));
                Assert.That(colliding, Does.Contain(gone));
            });

            var walked = false;
            foreach (var uid in colliding)
            {
                if (uid != gone)
                    continue;

                ent.DeleteEntity(gone);
                walked = true;
            }

            Assert.Multiple(() =>
            {
                Assert.That(walked, Is.True);
                Assert.That(colliding, Does.Contain(gone));
                Assert.That(stored["drop"], Is.EqualTo(gone));
                Assert.That(jukebox.AudioStream, Is.EqualTo(keeper));
            });
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            var jukebox = ent.GetComponent<JukeboxComponent>(owner);
            var stored = StoredItems(ent.GetComponent<ItemBorgModuleComponent>(owner));
            var colliding = Colliding(ent.GetComponent<StepTriggerComponent>(owner));
            Assert.Multiple(() =>
            {
                Assert.That(jukebox.AudioStream, Is.EqualTo(keeper));
                Assert.That(stored.ContainsKey("keep"), Is.True);
                Assert.That(stored["keep"], Is.EqualTo(keeper));
                Assert.That(stored.ContainsKey("drop"), Is.False);
                Assert.That(colliding, Does.Contain(keeper));
                Assert.That(colliding, Does.Not.Contain(gone));
            });

            ent.DeleteEntity(owner);
            ent.DeleteEntity(keeper);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnrelatedDirtiesAndJukeboxRefsStayCheap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var plainMs = 0d;
        var jukeboxMs = 0d;

        await server.WaitIdleAsync();
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var ent = server.EntMan;
            var stale = ent.System<StaleNetUidSystem>();
            var plain = new EntityUid[2000];
            for (var i = 0; i < plain.Length; i++)
                plain[i] = ent.SpawnEntity(null, MapCoordinates.Nullspace);

            var watch = Stopwatch.StartNew();
            foreach (var uid in plain)
                ent.Dirty(uid, ent.GetComponent<MetaDataComponent>(uid));
            stale.Flush();
            watch.Stop();
            plainMs = watch.Elapsed.TotalMilliseconds;

            foreach (var uid in plain)
                ent.DeleteEntity(uid);

            var owners = new EntityUid[300];
            var targets = new EntityUid[300];
            for (var i = 0; i < owners.Length; i++)
            {
                owners[i] = ent.SpawnEntity(null, MapCoordinates.Nullspace);
                targets[i] = ent.SpawnEntity(null, MapCoordinates.Nullspace);
                var jukebox = ent.EnsureComponent<JukeboxComponent>(owners[i]);
                SetField(jukebox, "AudioStream", targets[i]);
                ent.Dirty(owners[i], jukebox);
            }

            watch.Restart();
            stale.Flush();
            watch.Stop();
            jukeboxMs = watch.Elapsed.TotalMilliseconds;
            Assert.That(ent.GetComponent<JukeboxComponent>(owners[0]).AudioStream, Is.EqualTo(targets[0]));

            ent.DeleteEntity(targets[0]);
            stale.Flush();
            Assert.Multiple(() =>
            {
                Assert.That(ent.GetComponent<JukeboxComponent>(owners[0]).AudioStream, Is.Null);
                Assert.That(ent.GetComponent<JukeboxComponent>(owners[1]).AudioStream, Is.EqualTo(targets[1]));
            });

            for (var i = 0; i < owners.Length; i++)
            {
                ent.DeleteEntity(owners[i]);
                if (i != 0)
                    ent.DeleteEntity(targets[i]);
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(plainMs, Is.LessThan(25), $"Dirtying 2000 entities with no networked uid took {plainMs:0.00} ms.");
            Assert.That(jukeboxMs, Is.LessThan(40), $"Refreshing 300 jukebox streams took {jukeboxMs:0.00} ms.");
        });

        await pair.CleanReturnAsync();
    }

    private static void SetField(object component, string name, object value)
    {
        Field(component, name).SetValue(component, value);
    }

    private static Dictionary<string, EntityUid> StoredItems(object component)
    {
        return (Dictionary<string, EntityUid>) Field(component, "StoredItems").GetValue(component);
    }

    private static HashSet<EntityUid> Colliding(object component)
    {
        return (HashSet<EntityUid>) Field(component, "Colliding").GetValue(component);
    }

    private static FieldInfo Field(object component, string name)
    {
        return component.GetType().GetField(name);
    }
}
