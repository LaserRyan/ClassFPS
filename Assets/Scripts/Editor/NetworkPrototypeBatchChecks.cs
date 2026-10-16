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
                Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Exactly one active gameplay camera.");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "Exactly one active listener.");
                Check(!player.GetComponent<PlayerCombatInput>().enabled && !player.GetComponent<PlayerLifecycle>().enabled &&
                    !player.GetComponent<HeroSelection>().enabled, "Offline input/respawn/selection must stay off.");
                Check(player.GetComponentsInChildren<Weapon>(true).All(w => !w.enabled), "Online weapons must be inert.");
                Check(player.GetComponentsInChildren<HeroSecondary>(true).Length == 0 &&
                    player.GetComponentsInChildren<HeroPassive>(true).Length == 0, "No online abilities/passives.");
                Check(!player.GetComponent<PlayerHealth>().TakeDamage(1000f), "Offline damage cannot affect a network player.");
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
                manager.Shutdown();
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
                manager.Shutdown();
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
    private static void Finish(int result)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(result);
    }
}
