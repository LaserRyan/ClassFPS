using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opt-in: run in a disposable project, never the user's open scene.
[InitializeOnLoad]
public static class NetworkPrototypeBatchChecks
{
    private const string Key = "ClassFPS.NetworkBatchChecks";
    private static int stage, checks;
    private static double deadline, next;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static NetworkPrototypeBatchChecks()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        };
    }
    public static void BuildWirePlayer()
    {
        // This build is intentionally restricted to the disposable project.
        if (!Application.dataPath.Replace('\\', '/').Contains("/Builds/NetworkDashValidation/"))
            throw new InvalidOperationException("Run wire builds in the isolated validation copy.");
        var result = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { NetworkPrototypeSetup.ScenePath },
            locationPathName = System.IO.Path.GetFullPath("../NetworkWireBuild/ClassFPS.exe"),
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Wire player build failed.");
        Debug.Log("NETWORK_WIRE_BUILD_PASSED");
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/MovementTest.unity");
            NetworkPrototypeSetup.SetUp(); NetworkPrototypeSetup.SetUp();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static object Field(object actor, string name) => actor.GetType().GetField(name, Private).GetValue(actor);
    private static void Set(object actor, string name, object value) => actor.GetType().GetField(name, Private).SetValue(actor, value);
    private static object Call(object actor, string name, params object[] args) => actor.GetType().GetMethod(name, Private).Invoke(actor, args);
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static NetworkVariable<NetworkCombatSnapshot> Combat(NetworkPrototypePlayer actor) =>
        (NetworkVariable<NetworkCombatSnapshot>)Field(actor, "combatState");
    private static NetworkMoveCommand Command(NetworkPrototypePlayer actor, uint sequence, byte buttons = 16, float yaw = 0, float pitch = 0) =>
        new NetworkMoveCommand { Generation = Combat(actor).Value.Generation, Sequence = sequence, Buttons = buttons,
            Time = actor.NetworkManager.ServerTime.Time, Yaw = yaw, Pitch = pitch };
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Stage " + stage);
            if (EditorApplication.timeSinceStartup < next) return;
            var session = NetworkPrototypeSession.Active;
            if (session == null) return;
            NetworkManager manager = session.GetComponent<NetworkManager>();
            if (stage == 0) { session.StartHost(); stage++; return; }
            if (stage == 1)
            {
                if (session.LocalPlayer == null) return;
                var player = session.LocalPlayer;
                Check(player.IsOwner && !player.IsTestOpponent, "Host owns a human actor.");
                Check(player.GetComponentsInChildren<Camera>(true).Count(c => c.enabled) == 1, "One owner camera.");
                Check(player.GetComponentsInChildren<AudioListener>(true).Count(c => c.enabled) == 1, "One owner listener.");
                Check(!player.GetComponent<PlayerLifecycle>().enabled && !player.GetComponent<PlayerCombatInput>().enabled, "Offline lifecycle/combat inert online.");
                Check(player.GetComponent<PlayerHealth>().NetworkProtected, "Network health protected from local damage.");
                RunGeometryChecks(); RunMovementChecks(player);
                player.GetComponent<FPSController>().ResetAtSpawn(session.SpawnFor(player.OwnerClientId), Quaternion.Euler(0, session.SpawnYaw, 0));
                Physics.SyncTransforms();
                Set(session, "allowTestOpponent", false);
                Check(!(bool)Call(session, "HandleOpponent", player, (byte)0), "Server setting rejects opponent creation.");
                Set(session, "allowTestOpponent", true);
                Check((bool)Call(session, "HandleOpponent", player, (byte)0), "Development server creates opponent.");
                var bot = (NetworkPrototypePlayer)Field(session, "testOpponent");
                Check(bot != null && bot.IsTestOpponent && !bot.NetworkObject.IsPlayerObject, "Opponent is a server object, not a connection player.");
                Check(session.LocalPlayer == player, "Server-owned opponent cannot replace host ownership.");
                Check(!bot.GetComponentsInChildren<Camera>(true).Any(c => c.enabled) &&
                    !bot.GetComponentsInChildren<AudioListener>(true).Any(c => c.enabled), "Opponent has no local camera/listener.");
                Check(!bot.GetComponentsInChildren<WeaponAmmoHUD>(true).Any(h => h.enabled), "Opponent has no local ammo HUD.");
                Check((bool)Call(session, "HandleOpponent", player, (byte)0) && (NetworkPrototypePlayer)Field(session, "testOpponent") == bot, "Repeated spawn does not duplicate opponent.");
                RunCombatChecks(player, bot);
                Set(session, "allowTestHeroChanges", true);
                foreach (string id in new[] { "prototype.sword", "prototype.fire", "prototype.gun" })
                {
                    uint oldLife = Combat(player).Value.Generation;
                    Call(player, "ChangeHeroRpc", new FixedString64Bytes(id), oldLife, default(RpcParams));
                    Check(player.CurrentHeroId == id && Combat(player).Value.Generation > oldLife, "Server test hero change keeps identity/life synchronized.");
                    Check(player.GetComponentsInChildren<HeroKit>(false).Length == 1 && player.GetComponentsInChildren<WorldHeroPresentation>(false).Length == 1,
                        "Hero changes keep exactly one active kit/world appearance.");
                    var stale = Command(player, 999); stale.Generation = oldLife;
                    Call(player, "MoveInputRpc", stale, default(RpcParams));
                    Check((uint)Field(player, "acceptedInput") == 0, "Hero changes reject previous-kit movement/actions.");
                }
                Check((bool)Call(session, "HandleOpponent", player, (byte)4), "Opponent reset accepted.");
                Check(!Combat(bot).Value.Dead && Combat(bot).Value.Health == Combat(bot).Value.Maximum, "Opponent reset restores health.");
                session.Disconnect(); next = EditorApplication.timeSinceStartup + .5; stage++; return;
            }
            if (stage == 2)
            {
                if (manager.IsListening || manager.ShutdownInProgress) return;
                Check(session.LocalPlayer == null, "Disconnect clears local player.");
                Check(!UnityEngine.Object.FindObjectsByType<NetworkPrototypePlayer>(FindObjectsSortMode.None).Any(p => p.IsSpawned), "Disconnect removes human and opponent.");
                session.StartHost(); stage++; return;
            }
            if (stage == 3)
            {
                if (session.LocalPlayer == null) return;
                Check(manager.SpawnManager.SpawnedObjectsList.Count(o => o.GetComponent<NetworkPrototypePlayer>() != null) == 1, "Reconnect has one fresh human actor.");
                Debug.Log("NETWORK_NATIVE_CHECKS_PASSED " + checks);
                Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    private static void RunGeometryChecks()
    {
        var ray = new Ray(new Vector3(0, 1, -5), Vector3.forward);
        Check(Mathf.Abs(NetworkCapsuleQuery.RayDistance(ray, new Vector3(0, .4f, 0), new Vector3(0, 1.6f, 0), .4f) - 4.6f) < .001f, "Capsule body ray distance.");
        Check(float.IsPositiveInfinity(NetworkCapsuleQuery.RayDistance(new Ray(new Vector3(3, 1, -5), Vector3.forward), Vector3.zero, Vector3.up, .4f)), "Capsule miss.");
        Check(NetworkCapsuleQuery.RayDistance(new Ray(Vector3.up, Vector3.forward), Vector3.zero, Vector3.up * 2, .4f) == 0, "Inside capsule starts at zero.");
        Check(Math.Abs(NetworkCapsuleQuery.RewindTime(10, 1, .1, 1, .2) - 9.8) < .0001, "Old time bounded to maximum rewind.");
        Check(NetworkCapsuleQuery.RewindTime(10, 99, .1, 0, .2) == 10, "Future rewind time clamped.");
        Check(Math.Abs(NetworkCapsuleQuery.RewindTime(10, 1, .1, 0, .2) - 9.815) < .0001, "RTT budget restricts claimed old view time.");
        var c = new NetworkMoveCommand { Sequence = 1, Pitch = 89, Yaw = 90, X = 1 };
        Check(c.Valid, "Finite normalized command valid."); c.X = 2; Check(!c.Valid, "Excess movement magnitude rejected.");
        c.X = 0; c.Pitch = float.NaN; Check(!c.Valid, "NaN aim rejected."); c.Pitch = 90; Check(!c.Valid, "Out-of-range pitch rejected.");
        c.Pitch = 0; c.Time = double.PositiveInfinity; Check(!c.Valid, "Infinite timestamp rejected.");
    }
    private static void RunMovementChecks(NetworkPrototypePlayer actor)
    {
        FPSController motor = actor.GetComponent<FPSController>();
        Vector3 start = new Vector3(-40, 3, -40);
        motor.ResetAtSpawn(start, Quaternion.identity);
        NetworkMoveCommand command = Command(actor, 1); command.Z = 1;
        NetworkPlayerPose baseline = (NetworkPlayerPose)Call(motor, "MotorSnapshot", command.Generation, 0u, 0u, command.Time);
        for (int i = 0; i < 10; i++) Call(motor, "SimulateNetwork", command, 1f / 30, command.Time, null);
        Vector3 predicted = actor.transform.position;
        Check(predicted.z > start.z + 1, "Shared motor advances fixed-duration movement.");
        Call(motor, "RestoreMotor", baseline);
        for (int i = 0; i < 10; i++) Call(motor, "SimulateNetwork", command, 1f / 30, command.Time, null);
        Check(Vector3.Distance(actor.transform.position, predicted) < .005f, "Restore/replay produces same unobstructed state.");
        Call(actor, "ResetNetworkMovement");
        command = Command(actor, 1);
        Call(actor, "MoveInputRpc", command, default(RpcParams));
        Check((uint)Field(actor, "acceptedInput") == 1, "Server accepts owner command.");
        Call(actor, "MoveInputRpc", command, default(RpcParams));
        Check(((ICollection)Field(actor, "serverInputs")).Count == 1, "Duplicate command not queued.");
        command.Sequence = 2; command.Generation--;
        Call(actor, "MoveInputRpc", command, default(RpcParams));
        Check((uint)Field(actor, "acceptedInput") == 1, "Previous life command rejected.");
        command = Command(actor, 2); command.X = float.NaN;
        Call(actor, "MoveInputRpc", command, default(RpcParams));
        Check((uint)Field(actor, "acceptedInput") == 1, "Invalid command rejected before queue.");
        for (uint seq = 2; seq < 25; seq++) Call(actor, "MoveInputRpc", Command(actor, seq), default(RpcParams));
        Check(((ICollection)Field(actor, "serverInputs")).Count <= 4, "Flooded input cannot exceed the bounded burst budget.");
        Call(actor, "ResetNetworkMovement");
        var dash = (GunslingerDefinition)actor.GetComponent<HeroSelection>().SelectedHero.SecondaryActionPrefab.GetComponent<GunslingerDash>().Definition;
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            motor.ResetAtSpawn(start, Quaternion.identity);
            wall.transform.position = start + new Vector3(0, 1, 2); wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms(); command = Command(actor, 1, 24, 0, 80);
            Check((bool)Call(motor, "SimulateNetwork", command, .0333333f, command.Time, dash), "Airborne dash starts independently of pitch.");
            command.Buttons = 16; command.Yaw = 90;
            for (int i = 0; i < 8; i++) Call(motor, "SimulateNetwork", command, .0333333f, command.Time, dash);
            Check(actor.transform.position.z < start.z + 2, "Controller dash cannot pass through wall.");
            command.Buttons = 24;
            Check(!(bool)Call(motor, "SimulateNetwork", command, .0333333f, command.Time, dash), "Dash cooldown blocks second activation.");
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); }
        motor.ResetAtSpawn(start, Quaternion.identity); Call(actor, "ResetNetworkMovement");
    }
    private static void RunCombatChecks(NetworkPrototypePlayer shooter, NetworkPrototypePlayer target)
    {
        shooter.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-40, 3, -40), Quaternion.identity);
        target.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-40, 3, -32), Quaternion.identity);
        Call(shooter, "ResetServerCombat"); Call(target, "ResetServerCombat");
        ((IList)Field(shooter, "hitHistory")).Clear(); ((IList)Field(target, "hitHistory")).Clear();
        Call(shooter, "RecordHistory"); Call(target, "RecordHistory"); Physics.SyncTransforms();
        Call(shooter, "ServerDamage", 20f);
        Call(target, "ServerDamage", Combat(target).Value.Maximum - 10);
        float before = Combat(shooter).Value.Health;
        NetworkMoveCommand command = Command(shooter, 1, 18);
        Call(shooter, "ExecuteServerCombat", command);
        Check(Combat(target).Value.Dead, "Server ray kills opponent.");
        Check(Mathf.Abs(Combat(shooter).Value.Health - before - 1.5f) < .001f, "Killing hit heals only actual ten health removed.");
        int ammo = Combat(shooter).Value.Ammo;
        Call(shooter, "ExecuteServerCombat", command);
        Check(Combat(shooter).Value.Ammo == ammo, "Duplicate shot cannot consume ammo.");
        command.Sequence++; Call(shooter, "ExecuteServerCombat", command);
        Check(Combat(shooter).Value.Ammo == ammo, "Cooldown rejects excessive shots.");
        double deathAt = Combat(target).Value.RespawnAt;
        Call(target, "ServerDamage", 100f);
        Check(Combat(target).Value.RespawnAt == deathAt, "Repeated damage does not reschedule death.");
        Check(!target.GetComponent<CharacterController>().enabled, "Dead actor collision disabled.");
        Call(shooter, "ResetServerCombat"); Call(target, "SetOpponentAction", (byte)4);
        ((IList)Field(target, "hitHistory")).Clear();
        target.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-40, 3, -32), Quaternion.identity);
        Call(target, "RecordHistory");
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(-40, 4, -36); wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms(); before = Combat(target).Value.Health;
            Call(shooter, "ExecuteServerCombat", Command(shooter, 1, 18));
            Check(Combat(target).Value.Health == before, "Current world wall blocks compensated capsule.");
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); }
        Call(shooter, "ResetServerCombat");
        Call(target, "SetOpponentAction", (byte)4);
        target.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-35, 3, -32), Quaternion.identity);
        IList history = (IList)Field(target, "hitHistory"); history.Clear();
        Type frameType = history.GetType().GetGenericArguments()[0];
        double now = shooter.NetworkManager.ServerTime.Time;
        AddFrame(history, frameType, now - .1, new Vector3(-40, 3, -32));
        AddFrame(history, frameType, now, new Vector3(-35, 3, -32));
        before = Combat(target).Value.Health;
        Physics.SyncTransforms();
        Call(shooter, "ExecuteServerCombat", Command(shooter, 1, 18));
        Check(Combat(target).Value.Health < before, "Compensated ray hits historical moving capsule despite current-position miss.");
        uint oldGeneration = Combat(target).Value.Generation;
        Call(target, "SetOpponentAction", (byte)4);
        Check(Combat(target).Value.Generation > oldGeneration && history.Count <= 1, "Respawn clears previous-life capsule history.");
        Call(shooter, "ResetServerCombat");
        shooter.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-40, 3, -40), Quaternion.identity);
        target.GetComponent<FPSController>().ResetAtSpawn(new Vector3(-40, 3, -32), Quaternion.identity);
        ((IList)Field(shooter, "hitHistory")).Clear(); Call(shooter, "RecordHistory");
        Call(target, "SetOpponentAction", (byte)2);
        var botShot = (NetworkMoveCommand)Call(target, "OpponentCommand");
        Check(botShot.Has(NetworkMoveCommand.Fire) && !((NetworkMoveCommand)Call(target, "OpponentCommand")).Has(NetworkMoveCommand.Fire),
            "Opponent fire-once emits one intent without repeating it.");
        before = Combat(shooter).Value.Health; Physics.SyncTransforms();
        Call(target, "ExecuteServerCombat", botShot);
        Check(Combat(shooter).Value.Health < before, "Opponent shooting uses real server revolver damage.");
        Call(shooter, "ResetServerCombat");
    }
    private static void AddFrame(IList history, Type frameType, double time, Vector3 position)
    {
        object frame = Activator.CreateInstance(frameType);
        frameType.GetField("Time").SetValue(frame, time);
        frameType.GetField("A").SetValue(frame, position + Vector3.up * .4f);
        frameType.GetField("B").SetValue(frame, position + Vector3.up * 1.6f);
        frameType.GetField("Radius").SetValue(frame, .4f);
        history.Add(frame);
    }
    private static void Finish(int result)
    { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(result); }
}
