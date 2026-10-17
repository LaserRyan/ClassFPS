using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
[DisallowMultipleComponent]
public sealed class NetworkPrototypeSession : MonoBehaviour
{
    [SerializeField] private HeroDefinition[] heroes;
    [SerializeField] private NetworkPrototypePlayer playerPrefab;
    [SerializeField] private GameObject offlinePlayer;
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private Vector3 spawnOrigin = new Vector3(7f, 0.05f, 0f);
    [SerializeField] private float spawnYaw;
    [SerializeField, Min(2f)] private float spawnSpacing = 3f;
    [SerializeField] private bool allowTestHeroChanges;
    [SerializeField] private string address = "127.0.0.1";
    private NetworkManager manager;
    private readonly Dictionary<ulong, HeroDefinition> approved = new Dictionary<ulong, HeroDefinition>();
    private readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
    private readonly Dictionary<TrainingBot, bool> botStates = new Dictionary<TrainingBot, bool>();
    private int chosenHero;
    private bool connecting;
    private bool offline;
    private bool wasBackground;
    private string message;
    private Vector2 heroScroll;
    public static NetworkPrototypeSession Active { get; private set; }
    public HeroDefinition[] Heroes => heroes;
    public bool AllowTestHeroChanges => allowTestHeroChanges && NetworkTestRules.DevelopmentFeatures;
    public float SpawnYaw => spawnYaw;
    public NetworkPrototypePlayer LocalPlayer { get; set; }
    public void ReportStatus(string status) => message = status;
    private bool NetworkActive => manager.IsListening || connecting || manager.ShutdownInProgress;
    public Rect ConnectionPanelRect => new Rect(15f, 15f, 360f,
        offline ? 90f : NetworkActive ? 130f : Mathf.Min(470f, Screen.height - 30f));

    private void Awake()
    {
        Active = this;
        manager = GetComponent<NetworkManager>();
        if (offlinePlayer != null) offlinePlayer.SetActive(false);
        foreach (TrainingBot bot in FindObjectsByType<TrainingBot>(FindObjectsSortMode.None))
            botStates[bot] = bot.enabled;
        wasBackground = Application.runInBackground;
        Application.runInBackground = true;
        ShowLobbyCamera(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private void Start()
    {
        foreach (var pair in botStates) if (pair.Key != null) pair.Key.enabled = false;
    }
    private void Update()
    {
        // Runs before FPSController.Update: GUI clicks must not become capture clicks.
        if (LocalPlayer != null) LocalPlayer.GetComponent<FPSController>().CursorCaptureExclusion = ConnectionPanelRect;
        if (offline && offlinePlayer != null)
            offlinePlayer.GetComponent<FPSController>().CursorCaptureExclusion = ConnectionPanelRect;
    }
    private void OnEnable()
    {
        manager.ConnectionApprovalCallback = Approve;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        manager.OnClientStopped += Stopped;
        manager.OnServerStopped += Stopped;
    }
    private void OnDisable()
    {
        if (manager == null) return;
        manager.ConnectionApprovalCallback = null;
        manager.OnClientConnectedCallback -= Connected;
        manager.OnClientDisconnectCallback -= Disconnected;
        manager.OnClientStopped -= Stopped;
        manager.OnServerStopped -= Stopped;
        if (manager.IsListening) manager.Shutdown();
    }
    private void OnDestroy()
    {
        if (Active == this) Active = null;
        Application.runInBackground = wasBackground;
    }
    public HeroDefinition ResolveHero(string id)
    {
        if (string.IsNullOrEmpty(id) || Encoding.UTF8.GetByteCount(id) > 60 || heroes == null) return null;
        foreach (HeroDefinition hero in heroes) if (hero != null && hero.HeroId == id) return hero;
        return null;
    }
    public bool ValidateConfiguration(out string error)
    {
        error = null;
        if (playerPrefab == null || offlinePlayer == null || lobbyCamera == null || heroes == null || heroes.Length == 0)
        { error = "Run Set Up Local Multiplayer Test first."; return false; }
        var ids = new HashSet<string>();
        foreach (HeroDefinition hero in heroes)
        {
            if (!HeroSelection.IsValid(hero, out error)) return false;
            if (Encoding.UTF8.GetByteCount(hero.HeroId) > 60 || !ids.Add(hero.HeroId))
            { error = "Hero catalog needs unique IDs of at most 60 UTF-8 bytes."; return false; }
        }
        return true;
    }
    public Vector3 SpawnFor(ulong clientId) => spawnOrigin + Vector3.right * (slots[clientId] * Mathf.Max(2f, spawnSpacing));
    public void ShowLobbyCamera(bool show) { if (lobbyCamera != null) lobbyCamera.enabled = show; }
    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        HeroDefinition hero = request.Payload != null && request.Payload.Length <= 60 ?
            ResolveHero(Encoding.UTF8.GetString(request.Payload)) : null;
        response.Approved = hero != null;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = hero == null ? "Unknown hero ID. Use the same configured hero catalog on both instances." : "";
        if (hero != null) approved[request.ClientNetworkId] = hero;
    }
    private void Connected(ulong clientId)
    {
        if (clientId == manager.LocalClientId) { connecting = false; message = "Connected. Gunslinger primary/dash online; Fire and Sword attacks disabled."; }
        if (!manager.IsServer) return;
        if (!approved.TryGetValue(clientId, out HeroDefinition hero))
        { manager.DisconnectClient(clientId, "Missing approved hero."); return; }
        approved.Remove(clientId);
        int slot = 0;
        while (slots.ContainsValue(slot)) slot++;
        slots.Add(clientId, slot);
        NetworkPrototypePlayer player = Instantiate(playerPrefab, SpawnFor(clientId), Quaternion.Euler(0f, spawnYaw, 0f));
        player.InitializeServer(hero, SpawnFor(clientId), spawnYaw, AllowTestHeroChanges);
        player.NetworkObject.SpawnAsPlayerObject(clientId, true);
    }
    private void Disconnected(ulong clientId)
    {
        approved.Remove(clientId);
        slots.Remove(clientId);
        if (clientId == manager.LocalClientId)
            message = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected." : manager.DisconnectReason;
    }
    private void Stopped(bool wasHost)
    {
        if (message == "Disconnecting...") message = "Disconnected.";
        connecting = false;
        approved.Clear();
        slots.Clear();
        LocalPlayer = null;
        ShowLobbyCamera(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private void StartNetwork(bool host)
    {
        if (!ValidateConfiguration(out message)) return;
        if (manager.IsListening || manager.ShutdownInProgress || connecting) return;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.EnableSceneManagement = false; // Both instances explicitly load the same test scene.
        manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(heroes[Mathf.Clamp(chosenHero, 0, heroes.Length - 1)].HeroId);
        try
        {
            GetComponent<UnityTransport>().SetConnectionData(address, 7777, "0.0.0.0");
            connecting = true;
            message = host ? "Starting host..." : "Connecting...";
            bool started = host ? manager.StartHost() : manager.StartClient();
            if (!started) { connecting = false; message = "Could not start networking; check the Console/Player log."; }
        }
        catch (Exception error)
        {
            connecting = false;
            message = "Could not connect: " + error.Message;
            if (manager.IsListening) manager.Shutdown();
        }
    }
    public void StartHost() => StartNetwork(true);
    public void Join() => StartNetwork(false);
    public void Disconnect()
    {
        if (manager.ShutdownInProgress) return;
        message = "Disconnecting...";
        manager.Shutdown();
    }
    private void StartOffline()
    {
        offline = true;
        ShowLobbyCamera(false);
        foreach (var pair in botStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
        offlinePlayer.SetActive(true);
    }
    private void ReturnToLobby()
    {
        offlinePlayer.SetActive(false);
        foreach (var pair in botStates) if (pair.Key != null) pair.Key.enabled = false;
        foreach (StraightProjectile shot in FindObjectsByType<StraightProjectile>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
        offline = false;
        ShowLobbyCamera(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private void OnGUI()
    {
        bool networkActive = NetworkActive;
        GUILayout.BeginArea(ConnectionPanelRect, GUI.skin.box);
        if (offline)
        {
            GUILayout.Label("Offline test (Escape releases cursor)");
            if (GUILayout.Button("Return to connection menu")) ReturnToLobby();
        }
        else if (networkActive)
        {
            GUILayout.Label("Local multiplayer: server revolver combat");
            GUILayout.Label("Escape: cursor | F6: development hero test (if enabled)");
            if (GUILayout.Button("Disconnect / Cancel")) Disconnect();
        }
        else
        {
            GUILayout.Label("Choose a hero before connecting");
            heroScroll = GUILayout.BeginScrollView(heroScroll, GUILayout.MaxHeight(220f));
            if (heroes != null)
                for (int i = 0; i < heroes.Length; i++)
                    if (heroes[i] != null && GUILayout.Toggle(chosenHero == i, heroes[i].DisplayName, GUI.skin.button)) chosenHero = i;
            GUILayout.EndScrollView();
            GUILayout.Label("Server address (UDP 7777)");
            address = GUILayout.TextField(address);
            if (GUILayout.Button("Host")) StartNetwork(true);
            if (GUILayout.Button("Join")) StartNetwork(false);
            if (GUILayout.Button("Offline test")) StartOffline();
        }
        GUILayout.Label(message);
        GUILayout.EndArea();
    }
}
