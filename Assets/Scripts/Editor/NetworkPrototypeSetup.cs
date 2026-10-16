using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NetworkPrototypeSetup
{
    public const string ScenePath = "Assets/MultiplayerTest.unity";
    public const string PrefabPath = "Assets/Prefabs/Network/NetworkPlayer.prefab";

    [MenuItem("Tools/ClassFPS/Set Up Local Multiplayer Test")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.path != "Assets/MovementTest.unity") throw new InvalidOperationException("Open MovementTest or MultiplayerTest first.");
            if (scene.isDirty) throw new InvalidOperationException("Save current scene edits before creating the multiplayer copy.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                EditorSceneManager.SaveScene(scene, ScenePath, true);
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        HeroSelection[] selections = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HeroSelection>(true)).ToArray();
        if (selections.Length != 1) throw new InvalidOperationException("Expected exactly one offline Player with HeroSelection.");
        HeroSelection source = selections[0];
        foreach (HeroDefinition hero in source.Heroes)
            if (!HeroSelection.IsValid(hero, out string error)) throw new InvalidOperationException(error + " Run hero setup in MovementTest first.");
        Folder("Assets/Prefabs/Network");
        NetworkPrototypePlayer prefab = AssetDatabase.LoadAssetAtPath<NetworkPrototypePlayer>(PrefabPath);
        if (prefab == null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Existing NetworkPlayer prefab needs NetworkPrototypePlayer; it was not overwritten.");
            GameObject temporary = UnityEngine.Object.Instantiate(source.gameObject);
            temporary.name = "Network Player";
            try
            {
                temporary.SetActive(true);
                DisableLocalDefaults(temporary);
                temporary.AddComponent<NetworkPrototypePlayer>();
                var lifecycle = new SerializedObject(temporary.GetComponent<PlayerLifecycle>());
                lifecycle.FindProperty("spawnpoint").objectReferenceValue = null;
                lifecycle.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath).GetComponent<NetworkPrototypePlayer>();
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }
        NetworkPrototypeSession session = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NetworkPrototypeSession>(true)).SingleOrDefault();
        if (session == null) session = new GameObject("Network Prototype Session").AddComponent<NetworkPrototypeSession>();
        NetworkManager manager = session.GetComponent<NetworkManager>();
        if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = session.GetComponent<UnityTransport>();
        manager.NetworkConfig.PlayerPrefab = prefab.gameObject;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.EnableSceneManagement = false;
        manager.NetworkConfig.TickRate = 30;
        const string listPath = "Assets/Prefabs/Network/NetworkPrefabs.asset";
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
        if (list == null)
        { list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(list, listPath); }
        if (!list.Contains(prefab.gameObject)) { list.Add(new NetworkPrefab { Prefab = prefab.gameObject }); EditorUtility.SetDirty(list); }
        // NGO may auto-populate a DefaultNetworkPrefabs list when the component is added.
        // This test manager owns one explicit list; do not register its player twice.
        manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
        manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        Camera lobby = session.GetComponentInChildren<Camera>(true);
        if (lobby == null)
        {
            var view = new GameObject("Connection Menu Camera");
            view.transform.SetParent(session.transform, false);
            Camera sourceCamera = source.GetComponent<FPSController>().PlayerCamera;
            lobby = view.AddComponent<Camera>();
            EditorUtility.CopySerialized(sourceCamera, lobby);
            lobby.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
            lobby.enabled = true;
        }
        var data = new SerializedObject(session);
        data.FindProperty("playerPrefab").objectReferenceValue = prefab;
        data.FindProperty("offlinePlayer").objectReferenceValue = source.gameObject;
        data.FindProperty("lobbyCamera").objectReferenceValue = lobby;
        SerializedProperty catalog = data.FindProperty("heroes");
        catalog.arraySize = source.Heroes.Length;
        for (int i = 0; i < source.Heroes.Length; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = source.Heroes[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        source.gameObject.SetActive(false);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Validate();
        Selection.activeGameObject = session.gameObject;
        Debug.Log("MultiplayerTest saved. MovementTest was left intact. Build this scene on both peers; UDP 7777, localhost.");
    }
    private static void DisableLocalDefaults(GameObject root)
    {
        root.GetComponent<FPSController>().enabled = false;
        var movement = new SerializedObject(root.GetComponent<FPSController>());
        movement.FindProperty("ownsLocalInput").boolValue = false;
        movement.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<PlayerCombatInput>().enabled = false;
        root.GetComponent<PlayerLifecycle>().enabled = false;
        root.GetComponent<HeroSelection>().enabled = false;
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (MonoBehaviour behavior in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behavior is PlayerHealthHUD || behavior is PlayerDeathHUD || behavior is WeaponAmmoHUD ||
                behavior is CombatCrosshair || behavior is PlayerHealthDebugInput) behavior.enabled = false;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    [MenuItem("Tools/ClassFPS/Validate Local Multiplayer Test")]
    public static void Validate()
    {
        NetworkPrototypeSession[] sessions = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NetworkPrototypeSession>(true)).ToArray();
        if (sessions.Length != 1 || !sessions[0].ValidateConfiguration(out string error))
            throw new InvalidOperationException("Invalid multiplayer session/catalog. Check the configured references and hero assets.");
        if (sessions[0].GetComponent<NetworkManager>().NetworkConfig.Prefabs.NetworkPrefabsLists
            .Where(list => list != null).SelectMany(list => list.PrefabList)
            .GroupBy(entry => entry.Prefab).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Duplicate network prefab registration.");
        NetworkPrototypePlayer prefab = AssetDatabase.LoadAssetAtPath<NetworkPrototypePlayer>(PrefabPath);
        if (prefab == null || prefab.GetComponentsInChildren<NetworkObject>(true).Length != 1 ||
            prefab.GetComponent<FPSController>().enabled || prefab.GetComponent<HeroSelection>().enabled ||
            prefab.GetComponent<PlayerCombatInput>().enabled || prefab.GetComponent<PlayerLifecycle>().enabled ||
            new SerializedObject(prefab.GetComponent<FPSController>()).FindProperty("ownsLocalInput").boolValue ||
            prefab.GetComponentsInChildren<Camera>(true).Any(camera => camera.enabled) ||
            prefab.GetComponentsInChildren<AudioListener>(true).Any(listener => listener.enabled) ||
            prefab.GetComponentsInChildren<Weapon>(true).Length != 0)
            throw new InvalidOperationException("Network player prefab must start with inert local input/cameras/lifecycle and no scene weapons.");
        Debug.Log("Network catalog/prefab validation passed; this is not a two-instance gameplay test.");
    }
}
