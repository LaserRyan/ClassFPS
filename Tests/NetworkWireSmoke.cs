// Copied only into the disposable validation project's Assets by the wire-check procedure.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkWireSmoke : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool host, started, sawClient, driving;
    private double start, connected;
    private float clock, maximumTravel;
    private uint sequence = 1000;
    private NetworkPrototypeSession session;
    private NetworkPrototypePlayer actor;
    private Vector3 origin;
    private bool sawAmmoUse, sawDash;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (!args.Contains("--classfps-wire-host") && !args.Contains("--classfps-wire-client")) return;
        var runner = new GameObject("Network wire validation").AddComponent<NetworkWireSmoke>();
        runner.host = args.Contains("--classfps-wire-host"); runner.start = Time.realtimeSinceStartupAsDouble;
    }
    private static object Field(object o, string name) => o.GetType().GetField(name, Private).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    private void Update()
    {
        try
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - start > 35) throw new Exception("Wire smoke timed out.");
            if (!started)
            {
                session = NetworkPrototypeSession.Active; if (session == null) return;
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Wire fixture ground";
                ground.transform.position = new Vector3(-30, -.5f, -30); ground.transform.localScale = new Vector3(60, 1, 60);
                Set(session, "spawnOrigin", new Vector3(-30, .05f, -30));
                Set(session, "chosenHero", Array.FindIndex(session.Heroes, h => h.HeroId == "prototype.gun"));
                if (host) session.StartHost(); else session.Join();
                started = true; return;
            }
            NetworkManager manager = session.GetComponent<NetworkManager>();
            if (host)
            {
                foreach (NetworkObject obj in manager.SpawnManager.SpawnedObjectsList)
                {
                    NetworkPrototypePlayer remote = obj.GetComponent<NetworkPrototypePlayer>();
                    if (remote == null || remote.OwnerClientId == NetworkManager.ServerClientId) continue;
                    sawClient = true;
                    maximumTravel = Mathf.Max(maximumTravel, Vector3.Distance(remote.transform.position, new Vector3(-27, .05f, -30)));
                }
                if (sawClient && manager.ConnectedClients.Count == 1)
                {
                    if (maximumTravel < 3) throw new Exception("Server did not simulate remote movement.");
                    Debug.Log("NETWORK_WIRE_HOST_PASSED travel=" + maximumTravel);
                    manager.Shutdown(); Application.Quit(0);
                }
                return;
            }
            if (!driving)
            {
                actor = session.LocalPlayer; if (actor == null) return;
                // Disable graphical polling only. RPC reception and NV callbacks remain registered.
                actor.enabled = false; actor.GetComponent<FPSController>().enabled = false;
                origin = actor.transform.position; connected = now; driving = true;
            }
            double elapsed = now - connected;
            FPSController motor = actor.GetComponent<FPSController>();
            NetworkVariable<NetworkCombatSnapshot> combat = (NetworkVariable<NetworkCombatSnapshot>)Field(actor, "combatState");
            NetworkVariable<NetworkPlayerPose> pose = (NetworkVariable<NetworkPlayerPose>)Field(actor, "pose");
            sawAmmoUse |= combat.Value.Ammo < actor.GetComponent<HeroSelection>().SelectedKit.PrimaryWeapon.Ammo.Capacity;
            sawDash |= pose.Value.DashReady > 0;
            clock = Mathf.Min(clock + Time.unscaledDeltaTime, .1f);
            while (clock >= 1f / 30)
            {
                clock -= 1f / 30;
                var command = new NetworkMoveCommand { Generation = combat.Value.Generation, Sequence = ++sequence,
                    Time = manager.ServerTime.Time, X = elapsed < 1.5 ? 1 : 0, Yaw = 0, Pitch = 0,
                    Buttons = (byte)(16 | (elapsed < 1 ? 2 : 0) | (sequence == 1015 ? 8 : 0)) };
                GunslingerDefinition dash = actor.GetComponent<HeroSelection>().SelectedHero.SecondaryActionPrefab.GetComponent<GunslingerDash>().Definition;
                Call(motor, "SimulateNetwork", command, 1f / 30, command.Time, dash);
                ((IList)Field(actor, "predictions")).Add(command);
                Call(actor, "MoveInputRpc", command, default(RpcParams));
            }
            if (elapsed > 4)
            {
                float error = Vector3.Distance(actor.transform.position, pose.Value.Position);
                int pending = ((IList)Field(actor, "predictions")).Count;
                if (Vector3.Distance(origin, actor.transform.position) < 3 || pose.Value.Sequence < 1040 || error > .2f || pending > 8 || !sawAmmoUse || !sawDash)
                    throw new Exception($"Wire prediction mismatch: ack={pose.Value.Sequence} error={error} pending={pending} ammo={sawAmmoUse} dash={sawDash}");
                Debug.Log($"NETWORK_WIRE_CLIENT_PASSED ack={pose.Value.Sequence} error={error} pending={pending} ammo={sawAmmoUse} dash={sawDash}");
                manager.Shutdown(); Application.Quit(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
    }
}
#endif
