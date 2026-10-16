using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShootingTestSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Shooting Test")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up the scene.");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MovementTest")
            throw new InvalidOperationException("Open Assets/MovementTest.unity before running setup.");

        Transform[] objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        GameObject player = Find(objects, "Player");
        HeroPrototypeSetup.RequireLegacyMode(player);
        FPSController movement = player.GetComponent<FPSController>();
        if (movement == null || player.GetComponent<CharacterController>() == null ||
            player.GetComponentInChildren<Camera>() == null)
            throw new InvalidOperationException("Player needs its working FPSController, CharacterController, and child Camera.");

        string[] obstacleNames = { "Ground", "PlatformSmall", "PlatformTall", "Wall" };
        Renderer[] renderers = obstacleNames.Select(name => Find(objects, name).GetComponent<Renderer>()).ToArray();
        if (renderers.Any(renderer => renderer == null))
            throw new InvalidOperationException("Each obstacle needs a Renderer.");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("The URP/Lit shader could not be found.");

        // Reuse the existing Materials folder's casing (Windows paths are case insensitive).
        string materialFolder = AssetDatabase.GetSubFolders("Assets")
            .FirstOrDefault(path => string.Equals(path, "Assets/materials", StringComparison.OrdinalIgnoreCase));
        if (materialFolder == null)
        {
            AssetDatabase.CreateFolder("Assets", "materials");
            materialFolder = "Assets/materials";
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Shooting Test");

        Color[] colors = {
            new Color(0.42f, 0.44f, 0.46f), // neutral gray ground
            new Color(0.1f, 0.45f, 0.95f), // blue small platform
            new Color(1f, 0.48f, 0.08f),   // orange tall platform
            new Color(0.55f, 0.15f, 0.8f)  // purple wall
        };
        for (int i = 0; i < renderers.Length; i++)
        {
            Undo.RecordObject(renderers[i], "Assign obstacle material");
            renderers[i].sharedMaterial = MaterialFor(materialFolder, obstacleNames[i], colors[i], shader);
        }

        HitscanGun gun = player.GetComponentInChildren<HitscanGun>(true);
        if (gun == null)
        {
            var weaponObject = new GameObject("Hitscan Gun");
            Undo.RegisterCreatedObjectUndo(weaponObject, "Create gun");
            SceneManager.MoveGameObjectToScene(weaponObject, scene);
            weaponObject.transform.SetParent(player.transform, false);
            gun = Undo.AddComponent<HitscanGun>(weaponObject);
        }
        PlayerCombatInput input = player.GetComponent<PlayerCombatInput>();
        if (input == null)
            input = Undo.AddComponent<PlayerCombatInput>(player);
        Undo.RecordObject(input, "Equip gun");
        input.EquippedWeapon = gun;
        Undo.RecordObject(gun, "Enable gun attacks");
        gun.enabled = true;
        foreach (SwordWeapon sword in player.GetComponentsInChildren<SwordWeapon>(true))
        {
            Undo.RecordObject(sword, "Disable test sword");
            sword.enabled = false;
        }
        foreach (SwordPresentation view in player.GetComponentsInChildren<SwordPresentation>(true))
        {
            Undo.RecordObject(view.gameObject, "Hide sword visual");
            view.gameObject.SetActive(false);
        }
        foreach (GunPresentation view in player.GetComponentsInChildren<GunPresentation>(true))
        {
            Undo.RecordObject(view.gameObject, "Show gun visual");
            view.gameObject.SetActive(true);
        }
        if (player.GetComponent<CombatCrosshair>() == null)
            Undo.AddComponent<CombatCrosshair>(player);

        GameObject targets = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Shooting Targets");
        if (targets == null)
        {
            targets = new GameObject("Shooting Targets");
            Undo.RegisterCreatedObjectUndo(targets, "Create targets group");
            SceneManager.MoveGameObjectToScene(targets, scene);
        }
        // All targets rest on the existing ground. The last sits behind Wall from spawn.
        CreateTarget(targets.transform, "Target Red", new Vector3(-2f, 1f, 8f),
            MaterialFor(materialFolder, "TargetRed", new Color(0.95f, 0.08f, 0.1f), shader));
        CreateTarget(targets.transform, "Target Green", new Vector3(2f, 1f, 10f),
            MaterialFor(materialFolder, "TargetGreen", new Color(0.1f, 0.9f, 0.25f), shader));
        CreateTarget(targets.transform, "Target Behind Wall", new Vector3(-8f, 1f, 8f),
            MaterialFor(materialFolder, "TargetYellow", new Color(1f, 0.85f, 0.05f), shader));

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player;
        Debug.Log("Shooting test saved: gun equipped, crosshair attached, obstacles colored, three targets added. Enter Play mode to test.");
    }

    private static GameObject Find(Transform[] objects, string name)
    {
        Transform found = objects.FirstOrDefault(item => item.name == name);
        if (found == null)
            throw new InvalidOperationException("Missing scene object: " + name);
        return found.gameObject;
    }

    private static Material MaterialFor(string folder, string name, Color color, Shader shader)
    {
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CreateTarget(Transform parent, string name, Vector3 position, Material material)
    {
        if (parent.Find(name) != null)
            return; // Running setup again preserves existing targets and health settings.

        var target = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(target, "Create target");
        SceneManager.MoveGameObjectToScene(target, parent.gameObject.scene);
        target.transform.SetParent(parent, false);
        target.transform.position = position;
        Undo.AddComponent<TargetHealth>(target);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(visual, "Create target collider and visual");
        visual.name = "Visual and Collider";
        visual.transform.SetParent(target.transform, false);
        visual.transform.localScale = new Vector3(1f, 2f, 1f);
        visual.GetComponent<Renderer>().sharedMaterial = material;
    }
}
