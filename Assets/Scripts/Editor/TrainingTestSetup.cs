using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingTestSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Respawn and Training Bot")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before training setup.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/MovementTest.unity")
            throw new InvalidOperationException("Open Assets/MovementTest.unity first.");
        FPSController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<FPSController>(true)).ToArray();
        TrainingBot[] bots = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TrainingBot>(true)).ToArray();
        if (players.Length != 1 || bots.Length > 1)
            throw new InvalidOperationException("Setup requires one player and at most one training bot.");
        if (players[0].GetComponentsInChildren<PlayerDeathHUD>(true).Length > 1)
            throw new InvalidOperationException("Keep only one PlayerDeathHUD before running setup.");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            throw new InvalidOperationException("URP Lit shader is required.");
        PlayerHealthSetup.SetUp(); // Reuses existing health/HUD and preserves configured values.
        FPSController player = players[0];
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Respawn and Training Bot");
        GameObject setup = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Training Test");
        if (setup == null)
            setup = Child("Training Test", null, scene);
        PlayerLifecycle lifecycle = player.GetComponent<PlayerLifecycle>();
        bool newLifecycle = lifecycle == null;
        if (newLifecycle)
            lifecycle = Undo.AddComponent<PlayerLifecycle>(player.gameObject);
        var lifeData = new SerializedObject(lifecycle);
        Transform spawn = lifeData.FindProperty("spawnpoint").objectReferenceValue as Transform;
        if (spawn == null)
        {
            spawn = setup.transform.Find("Player Spawnpoint");
            if (spawn == null)
            {
                spawn = Child("Player Spawnpoint", setup.transform, scene).transform;
                spawn.SetPositionAndRotation(new Vector3(7f, 0.05f, 0f), Quaternion.identity);
            }
            lifeData.FindProperty("spawnpoint").objectReferenceValue = spawn;
            lifeData.ApplyModifiedProperties();
        }
        if (newLifecycle)
        {
            Undo.RecordObject(player.transform, "Place player at safe spawn");
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        Undo.RecordObject(lifecycle, "Enable player lifecycle");
        lifecycle.enabled = true;
        PlayerDeathHUD[] deathViews = player.GetComponentsInChildren<PlayerDeathHUD>(true);
        if (deathViews.Length > 1)
            throw new InvalidOperationException("Keep only one PlayerDeathHUD before running setup.");
        PlayerDeathHUD deathView = deathViews.FirstOrDefault();
        if (deathView == null)
            deathView = Undo.AddComponent<PlayerDeathHUD>(Child("Death HUD", player.transform, scene));
        var viewData = new SerializedObject(deathView);
        viewData.FindProperty("lifecycle").objectReferenceValue = lifecycle;
        viewData.ApplyModifiedProperties();
        Undo.RecordObject(deathView, "Enable death HUD");
        deathView.enabled = true;
        Undo.RecordObject(deathView.gameObject, "Show death HUD");
        deathView.gameObject.SetActive(true);

        Material body = MaterialFor("TrainingBotBody", lit, new Color(0.1f, 0.65f, 0.75f));
        Material trim = MaterialFor("TrainingBotTrim", lit, new Color(0.15f, 0.18f, 0.22f));
        Material shotMaterial = MaterialFor("TrainingProjectile", lit, new Color(1f, 0.35f, 0.05f));
        StraightProjectile prefab = ProjectilePrefab(shotMaterial, scene);
        TrainingBot bot = bots.FirstOrDefault();
        if (bot == null)
        {
            GameObject root = Child("Training Bot", setup.transform, scene);
            root.transform.SetPositionAndRotation(new Vector3(10f, 0f, 8f), Quaternion.Euler(0f, 180f, 0f));
            Undo.AddComponent<PlayerHealth>(root);
            bot = Undo.AddComponent<TrainingBot>(root);
            Shape("Body Hitbox", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1.2f, 0.6f), body);
            Shape("Head Hitbox", root.transform, new Vector3(0f, 1.85f, 0f), new Vector3(0.6f, 0.45f, 0.6f), body);
            Shape("Base", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.1f, 0.4f, 0.8f), trim);
            Shape("Forward Barrel", root.transform, new Vector3(0f, 1.3f, 0.5f), new Vector3(0.22f, 0.22f, 0.65f), trim);
            Transform muzzle = Child("Muzzle", root.transform, scene).transform;
            muzzle.localPosition = new Vector3(0f, 1.3f, 0.9f);
            var botData = new SerializedObject(bot);
            botData.FindProperty("muzzle").objectReferenceValue = muzzle;
            botData.FindProperty("projectilePrefab").objectReferenceValue = prefab;
            botData.ApplyModifiedProperties();
        }
        else
        {
            var botData = new SerializedObject(bot);
            if (botData.FindProperty("projectilePrefab").objectReferenceValue == null)
            {
                botData.FindProperty("projectilePrefab").objectReferenceValue = prefab;
                botData.ApplyModifiedProperties();
            }
        }
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = bot.gameObject;
        Debug.Log("Training test saved. Spawn x=7 is outside the firing lane x=10. Bot faces -Z; walk right into its lane or approach from the side.");
    }

    private static GameObject Child(string name, Transform parent, Scene scene)
    {
        var child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create training object");
        SceneManager.MoveGameObjectToScene(child, scene);
        if (parent != null)
            child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(shape, "Create bot shape");
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }

    private static Material MaterialFor(string name, Shader shader, Color color)
    {
        string folder = AssetDatabase.GetSubFolders("Assets").FirstOrDefault(path =>
            string.Equals(path, "Assets/materials", StringComparison.OrdinalIgnoreCase));
        if (folder == null)
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
            folder = "Assets/Materials";
        }
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static StraightProjectile ProjectilePrefab(Material material, Scene scene)
    {
        const string path = "Assets/Prefabs/TrainingProjectile.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            StraightProjectile component = existing.GetComponent<StraightProjectile>();
            if (component == null)
                throw new InvalidOperationException("TrainingProjectile.prefab needs a StraightProjectile component.");
            return component;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        try
        {
            SceneManager.MoveGameObjectToScene(temporary, scene);
            temporary.name = "Training Projectile";
            temporary.layer = LayerMask.NameToLayer("Ignore Raycast");
            temporary.transform.localScale = Vector3.one * 0.24f;
            UnityEngine.Object.DestroyImmediate(temporary.GetComponent<Collider>());
            temporary.GetComponent<Renderer>().sharedMaterial = material;
            temporary.AddComponent<StraightProjectile>();
            return PrefabUtility.SaveAsPrefabAsset(temporary, path).GetComponent<StraightProjectile>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }
}
