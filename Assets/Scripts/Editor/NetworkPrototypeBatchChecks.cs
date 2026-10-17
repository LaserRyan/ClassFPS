using System;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opt-in CLI check. Run only in an isolated copy: generates/saves the multiplayer scene.
[InitializeOnLoad]
public static class NetworkPrototypeBatchChecks
{
    private const string Key = "ClassFPS.NetworkBatchChecks";
    private static int stage;
    private static int checks;
    private static double deadline;
    private static double nextStageAt;
    private static string expectedHero;
    static NetworkPrototypeBatchChecks()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
            {
                deadline = EditorApplication.timeSinceStartup + 40;
                EditorApplication.update += Tick;
            }
        };
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/MovementTest.unity");
            NetworkPrototypeSetup.SetUp();
            NetworkPrototypeSetup.SetUp();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Native network host checks timed out at stage " + stage);
            if (EditorApplication.timeSinceStartup < nextStageAt) return;
            NetworkPrototypeSession session = NetworkPrototypeSession.Active;
            if (session == null) return;
            NetworkManager manager = session.GetComponent<NetworkManager>();
            if (stage == 0)
            {
                Check(!session.AllowTestHeroChanges, "Test hero changes must default off.");
                Check(session.ResolveHero("unknown.hero") == null, "Unknown hero resolution must fail.");
                var data = new SerializedObject(session);
                data.FindProperty("allowTestHeroChanges").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                session.StartHost();
                stage++;
            }
            else if (stage == 1 && session.LocalPlayer != null && session.LocalPlayer.IsSpawned)
            {
                NetworkPrototypePlayer player = session.LocalPlayer;
                Check(manager.ConnectedClients.Count == 1, "One connected host expected.");
                Check(player.IsOwner && player.GetComponent<FPSController>().enabled, "Host owns movement.");
                FPSController movement = player.GetComponent<FPSController>();
                movement.CursorCaptureExclusion = session.ConnectionPanelRect;
                Check(!movement.CanCaptureCursorAt(new Vector2(60f, Screen.height - 80f)), "Disconnect-panel clicks cannot recapture the cursor.");
                Check(movement.CanCaptureCursorAt(new Vector2(400f, Screen.height - 80f)), "Clicks outside the panel can recapture normally.");
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Exactly one active gameplay camera.");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Exactly one active listener.");
                Check(!player.GetComponent<PlayerCombatInput>().enabled && !player.GetComponent<PlayerLifecycle>().enabled &&
                    !player.GetComponent<HeroSelection>().enabled, "Offline input/respawn/selection must stay off.");
                Check(player.GetComponentsInChildren<Weapon>(true).All(w => !w.enabled), "Online weapons must be inert.");
                Check(player.GetComponentsInChildren<HeroSecondary>(true).Length == 0 &&
                    player.GetComponentsInChildren<HeroPassive>(true).Length == 0, "No online abilities/passives.");
                Check(!player.GetComponent<PlayerHealth>().TakeDamage(1000f), "Offline damage cannot affect a network player.");
                RunCombatChecks(player, session);
                expectedHero = session.Heroes.First(h => h.HeroId != player.CurrentHeroId).HeroId;
                typeof(NetworkPrototypePlayer).GetMethod("SetTestMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { true });
                Check(!player.GetComponent<FPSController>().CanAcceptGameplayInput, "Test menu suppresses gameplay input.");
                typeof(NetworkPrototypePlayer).GetMethod("RequestHero", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { expectedHero });
                nextStageAt = EditorApplication.timeSinceStartup + .5;
                stage++;
            }
            else if (stage == 2)
            {
                NetworkPrototypePlayer player = session.LocalPlayer;
                Check(player.CurrentHeroId == expectedHero, "Server accepted and synchronized the requested hero.");
                Check(player.GetComponent<HeroSelection>().SelectedHero.HeroId == expectedHero, "Selected runtime appearance changed.");
                Check(player.GetComponentsInChildren<HeroKit>(true).Length == 1 &&
                    player.GetComponentsInChildren<WorldHeroPresentation>(true).Length == 1, "No old kit/model survives hero change.");
                Check(player.GetComponent<PlayerHealth>().CurrentHealth == session.ResolveHero(expectedHero).MaximumHealth, "Hero change restores configured health.");
                // Try a stale revision through the real generated owner-to-server RPC.
                typeof(NetworkPrototypePlayer).GetMethod("ChangeHeroRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { new FixedString64Bytes(session.Heroes[0].HeroId), 1u, default(RpcParams) });
                Check(player.CurrentHeroId == expectedHero, "Stale hero request rejected.");
                typeof(NetworkPrototypePlayer).GetMethod("ChangeHeroRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { new FixedString64Bytes("unknown.hero"), 2u, default(RpcParams) });
                Check(player.CurrentHeroId == expectedHero, "Unknown hero RPC rejected.");
                var settings = new SerializedObject(session);
                settings.FindProperty("allowTestHeroChanges").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                typeof(NetworkPrototypePlayer).GetMethod("ChangeHeroRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { new FixedString64Bytes(session.Heroes[0].HeroId), 2u, default(RpcParams) });
                Check(player.CurrentHeroId == expectedHero, "Disabled server setting rejects actual RPC.");
                settings.FindProperty("allowTestHeroChanges").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                expectedHero = session.Heroes.Last().HeroId;
                typeof(NetworkPrototypePlayer).GetMethod("RequestHero", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { expectedHero });
                nextStageAt = EditorApplication.timeSinceStartup + .5;
                stage = 6;
            }
            else if (stage == 6)
            {
                Check(session.LocalPlayer.CurrentHeroId == expectedHero, "Repeated change applies third hero.");
                Check(session.LocalPlayer.GetComponentsInChildren<HeroKit>(true).Length == 1 &&
                    session.LocalPlayer.GetComponentsInChildren<WorldHeroPresentation>(true).Length == 1, "Repeated changes leave one kit/model.");
                session.Disconnect();
                nextStageAt = EditorApplication.timeSinceStartup + 1;
                stage = 3;
            }
            else if (stage == 3 && !manager.IsListening && !manager.ShutdownInProgress)
            {
                Check(session.LocalPlayer == null && UnityEngine.Object.FindObjectsByType<NetworkPrototypePlayer>(FindObjectsSortMode.None).Length == 0,
                    "Disconnect cleans all player instances.");
                session.StartHost();
                stage++;
            }
            else if (stage == 4 && session.LocalPlayer != null && session.LocalPlayer.IsSpawned)
            {
                Check(manager.ConnectedClients.Count == 1 && UnityEngine.Object.FindObjectsByType<NetworkPrototypePlayer>(FindObjectsSortMode.None).Length == 1,
                    "Reconnect creates exactly one fresh player.");
                session.Disconnect();
                stage++;
                nextStageAt = EditorApplication.timeSinceStartup + 1;
            }
            else if (stage == 5 && !manager.IsListening && !manager.ShutdownInProgress)
            {
                Debug.Log("NETWORK_BATCH_CHECKS_PASSED: " + checks);
                Finish(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static object Call(NetworkPrototypePlayer player, string method, params object[] arguments) =>
        typeof(NetworkPrototypePlayer).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, arguments);
    private static void Fire(NetworkPrototypePlayer shooter, uint generation, uint sequence, Vector3 aim, bool reload,
        NetworkPlayerPose? sample = null)
    {
        NetworkPlayerPose body = sample ?? (NetworkPlayerPose)Call(shooter, "CapturePose");
        body.Revision = generation;
        Call(shooter, "RevolverIntentRpc", generation, sequence, body, aim, reload, default(RpcParams));
    }
    private static void RunCombatChecks(NetworkPrototypePlayer shooter, NetworkPrototypeSession session)
    {
        Check(shooter.CurrentHeroId == "prototype.gun", "Native combat check starts with Gunslinger.");
        NetworkPrototypePlayer prefab = AssetDatabase.LoadAssetAtPath<NetworkPrototypePlayer>(NetworkPrototypeSetup.PrefabPath);
        NetworkPrototypePlayer target = UnityEngine.Object.Instantiate(prefab);
        GameObject wall = null;
        try
        {
            target.InitializeServer(session.ResolveHero("prototype.fire"), new Vector3(-20, 1, -16), 0, false);
            target.NetworkObject.Spawn();
            // A second server-owned actor is a test fixture, not an extra local controller/view.
            Call(target, "SetLocalComponents", false);
            target.GetComponent<FPSController>().SetLocalInputOwnership(false);
            session.LocalPlayer = shooter;
            CheckRemotePoseApplication(target);
            shooter.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-20, 1, -20), Quaternion.identity);
            var heroVariable = (NetworkVariable<NetworkHeroState>)typeof(NetworkPrototypePlayer)
                .GetField("hero", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shooter);
            uint generation = heroVariable.Value.Revision;
            HitscanGun gun = shooter.GetComponent<HeroSelection>().SelectedKit.PrimaryWeapon as HitscanGun;
            int effects = 0;
            gun.ShotFired += endpoint => effects++;
            int initialAmmo = gun.Ammo.CurrentAmmo;
            float initialTargetHealth = target.GetComponent<PlayerHealth>().CurrentHealth;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-20, 2, -18);
            wall.transform.localScale = new Vector3(2, 4, .3f);
            Fire(shooter, generation, 1u, Vector3.forward, false);
            Check(target.GetComponent<PlayerHealth>().CurrentHealth == initialTargetHealth, "Server wall blocks damage.");
            Check(gun.Ammo.CurrentAmmo == initialAmmo - 1 && effects == 1, "Host accepted shot consumes once and presents once.");
            Fire(shooter, generation, 1u, Vector3.forward, false);
            Fire(shooter, generation, 2u, Vector3.forward, false);
            Check(gun.Ammo.CurrentAmmo == initialAmmo - 1 && effects == 1, "Duplicate/flood/cooldown requests cannot shoot twice.");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            Call(shooter, "ResetServerCombat");
            Fire(shooter, generation, 1u, new Vector3(float.NaN, 0, 1), false);
            Check(gun.Ammo.CurrentAmmo == initialAmmo, "Non-finite aim does not consume ammo.");
            Call(shooter, "ResetServerCombat");
            NetworkPlayerPose invalidBody = (NetworkPlayerPose)Call(shooter, "CapturePose");
            invalidBody.Position.x = float.PositiveInfinity;
            Fire(shooter, generation, 1u, Vector3.forward, false, invalidBody);
            Check(gun.Ammo.CurrentAmmo == initialAmmo, "Non-finite shot body position is rejected.");
            Call(shooter, "ResetServerCombat");
            invalidBody = (NetworkPlayerPose)Call(shooter, "CapturePose");
            invalidBody.Revision = generation - 1;
            Call(shooter, "RevolverIntentRpc", generation, 1u, invalidBody, Vector3.forward, false, default(RpcParams));
            Check(gun.Ammo.CurrentAmmo == initialAmmo, "Shot and embedded body generation must match.");
            Call(shooter, "ResetServerCombat");
            Call(shooter, "ServerDamage", shooter.GetComponent<PlayerHealth>().MaximumHealth - 50);
            Call(target, "ServerDamage", initialTargetHealth - 10);
            typeof(NetworkPrototypePlayer).GetField("networkRespawnDelay", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 0f);
            // Reproduce a moving owner whose latest shot pose is ahead of the server copy.
            NetworkPlayerPose matchingShot = (NetworkPlayerPose)Call(shooter, "CapturePose");
            shooter.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-22, 1, -20), Quaternion.identity);
            Fire(shooter, generation, 1u, Vector3.forward, false, matchingShot);
            Vector3 origin = (Vector3)typeof(NetworkPrototypePlayer).GetProperty("LastServerShotOrigin",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shooter);
            Check(Mathf.Abs(origin.x - matchingShot.Position.x) < .001f && Mathf.Abs(origin.x - shooter.transform.position.x) > 1,
                "Moving shot uses its matching body snapshot, not the delayed server transform.");
            Check(target.GetComponent<PlayerHealth>().CurrentHealth == 0, "Server hitscan kills a different hero.");
            Check(Mathf.Abs(shooter.GetComponent<PlayerHealth>().CurrentHealth - 51.5f) < .001f, "Network killing-hit lifesteal uses actual ten health removed.");
            Check(!target.GetComponent<CharacterController>().enabled, "Dead actor collider disabled.");
            Check(!target.GetComponent<FPSController>().CanAcceptGameplayInput, "Dead actor cannot accept movement input.");
            Check((float)Call(target, "ServerDamage", 25f) == 0, "Duplicate death damage rejected.");
            Call(target, "TickServerCombat");
            Check(target.GetComponent<PlayerHealth>().CurrentHealth == initialTargetHealth && target.GetComponent<CharacterController>().enabled,
                "Server deadline respawns and restores health/collider.");
            Check(target.GetComponent<HeroSelection>().SelectedHero.HeroId == "prototype.fire", "Death keeps selected hero.");
            Call(shooter, "ResetServerCombat");
            Fire(shooter, generation - 1, 500u, Vector3.forward, false);
            Check(gun.Ammo.CurrentAmmo == initialAmmo, "Stale life fire rejected through real RPC.");
        }
        finally
        {
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (target != null && target.IsSpawned) target.NetworkObject.Despawn(true);
            session.LocalPlayer = shooter;
            shooter.GetComponent<FPSController>().ResetAtSpawn(session.SpawnFor(shooter.OwnerClientId), Quaternion.Euler(0, session.SpawnYaw, 0));
        }
    }
    private static void CheckRemotePoseApplication(NetworkPrototypePlayer actor)
    {
        // Exercise the non-owner branch in a native single-host fixture; this is not a wire client test.
        PropertyInfo ownership = typeof(NetworkBehaviour).GetProperty("IsOwner");
        bool wasOwner = actor.IsOwner;
        Vector3 start = actor.transform.position;
        WorldHeroPresentation world = actor.GetComponent<HeroSelection>().SelectedWorldPresentation;
        Vector3 visualStart = world.transform.position;
        var heroVariable = (NetworkVariable<NetworkHeroState>)typeof(NetworkPrototypePlayer)
            .GetField("hero", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
        uint revision = heroVariable.Value.Revision;
        try
        {
            ownership.SetValue(actor, false);
            var newest = new NetworkPlayerPose { Position = start + Vector3.right, Revision = revision, Sequence = 2 };
            Call(actor, "ApplyRemotePose", newest);
            Check(Vector3.Distance(actor.transform.position, newest.Position) < .001f,
                "Non-owner collision root immediately uses newest received pose.");
            Check(Vector3.Distance(world.transform.position, visualStart) < .001f,
                "Root update preserves world visual position for independent smoothing.");
            Call(actor, "ApplyRemotePose", new NetworkPlayerPose { Position = start, Revision = revision, Sequence = 1 });
            Check(Vector3.Distance(actor.transform.position, newest.Position) < .001f,
                "Delayed movement packet cannot roll collision state back.");
            Call(actor, "ApplyRemotePose", new NetworkPlayerPose { Position = start, Revision = revision + 1, Sequence = 999 });
            Check(Vector3.Distance(actor.transform.position, newest.Position) < .001f,
                "Future-generation pose cannot move current life.");
            Call(actor, "SmoothRemotePresentation");
            Check(world.transform.position.x >= visualStart.x && world.transform.position.x < newest.Position.x,
                "Visible model smooths independently while collision root remains current.");
        }
        finally
        {
            ownership.SetValue(actor, wasOwner);
            actor.GetComponent<FPSController>().ResetAtSpawn(start, Quaternion.identity);
        }
    }
    private static void Finish(int result)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(result);
    }
}
