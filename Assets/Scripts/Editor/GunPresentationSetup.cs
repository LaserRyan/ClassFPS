using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GunPresentationSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Gun Presentation")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before adding gun presentation.");

        Scene scene = SceneManager.GetActiveScene();
        PlayerCombatInput[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCombatInput>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Open the shooting scene with exactly one PlayerCombatInput.");

        PlayerCombatInput input = players[0];
        HeroPrototypeSetup.RequireLegacyMode(input.gameObject);
        HitscanGun gun = input.EquippedWeapon as HitscanGun;
        FPSController controller = input.GetComponent<FPSController>();
        Camera camera = controller != null ? controller.PlayerCamera : null;
        if (gun == null || camera == null)
            throw new InvalidOperationException("Player needs its equipped HitscanGun and assigned Main Camera.");

        GunPresentation existing = camera.GetComponentInChildren<GunPresentation>(true);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Gun presentation already exists; selected it without changing its configuration.");
            return;
        }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (lit == null || unlit == null)
            throw new InvalidOperationException("URP Lit and Unlit shaders are required.");

        string folder = AssetDatabase.GetSubFolders("Assets").FirstOrDefault(path =>
            string.Equals(path, "Assets/materials", StringComparison.OrdinalIgnoreCase));
        if (folder == null)
        {
            AssetDatabase.CreateFolder("Assets", "materials");
            folder = "Assets/materials";
        }
        Material body = MaterialFor(folder, "GunBody", lit, new Color(0.17f, 0.2f, 0.25f));
        Material trim = MaterialFor(folder, "GunTrim", lit, new Color(0.48f, 0.53f, 0.6f));
        Material flashMaterial = MaterialFor(folder, "GunFlash", unlit, new Color(1f, 0.8f, 0.2f));
        Material tracerMaterial = MaterialFor(folder, "GunTracer", unlit, new Color(1f, 0.65f, 0.1f));

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Gun Presentation");
        GameObject view = Child("Gun Presentation", camera.transform);
        // Keep every vertex in front of the near plane, even at maximum default recoil.
        Vector3 position = new Vector3(0.23f, -0.18f, Mathf.Max(0.5f, camera.nearClipPlane + 0.3f));
        view.transform.localPosition = position;
        GameObject model = Child("Placeholder Model", view.transform);
        Shape("Receiver", model.transform, new Vector3(0f, 0f, 0.08f), new Vector3(0.14f, 0.12f, 0.36f), body);
        Shape("Barrel", model.transform, new Vector3(0f, 0.015f, 0.34f), new Vector3(0.07f, 0.07f, 0.2f), trim);
        Shape("Grip", model.transform, new Vector3(0f, -0.12f, 0.02f), new Vector3(0.09f, 0.18f, 0.12f), body);
        Shape("Sight", model.transform, new Vector3(0f, 0.085f, 0.19f), new Vector3(0.025f, 0.05f, 0.04f), trim);
        GameObject muzzle = Child("Muzzle", view.transform);
        muzzle.transform.localPosition = new Vector3(0f, 0.015f, 0.45f);
        Renderer flash = Shape("Muzzle Flash", muzzle.transform, new Vector3(0f, 0f, 0.035f),
            new Vector3(0.09f, 0.09f, 0.1f), flashMaterial);
        flash.enabled = false;

        GameObject lineObject = Child("Shot Tracer", view.transform);
        LineRenderer line = Undo.AddComponent<LineRenderer>(lineObject);
        line.sharedMaterial = tracerMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = 0.012f;
        line.endWidth = 0.004f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        GunPresentation presentation = Undo.AddComponent<GunPresentation>(view);
        var serialized = new SerializedObject(presentation);
        serialized.FindProperty("gun").objectReferenceValue = gun;
        serialized.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
        serialized.FindProperty("muzzleFlash").objectReferenceValue = flash;
        serialized.FindProperty("tracer").objectReferenceValue = line;
        serialized.FindProperty("weaponPosition").vector3Value = position;
        serialized.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = view;
        Debug.Log("Gun presentation saved. Existing weapon, movement, targets, and camera settings were preserved.");
    }

    private static GameObject Child(string name, Transform parent)
    {
        var child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create gun visual");
        SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
        child.transform.SetParent(parent, false);
        child.layer = LayerMask.NameToLayer("Ignore Raycast");
        return child;
    }

    private static Renderer Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(shape, "Create gun shape");
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Collider collider in shape.GetComponents<Collider>())
            Undo.DestroyObjectImmediate(collider);
        Renderer renderer = shape.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Material MaterialFor(string folder, string name, Shader shader, Color color)
    {
        string path = folder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
