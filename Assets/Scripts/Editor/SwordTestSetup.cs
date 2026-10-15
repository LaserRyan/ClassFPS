using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SwordTestSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Sword Test")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up the sword.");
        Scene scene = SceneManager.GetActiveScene();
        PlayerCombatInput[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCombatInput>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Open MovementTest with exactly one PlayerCombatInput.");
        PlayerCombatInput input = players[0];
        Camera camera = input.GetComponent<FPSController>().PlayerCamera;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (camera == null || shader == null)
            throw new InvalidOperationException("An assigned player camera and URP Lit shader are required.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Sword Test");
        SwordWeapon sword = input.GetComponent<SwordWeapon>();
        if (sword == null)
            sword = Undo.AddComponent<SwordWeapon>(input.gameObject);
        Undo.RecordObject(sword, "Enable sword");
        sword.enabled = true;
        Undo.RecordObject(input, "Equip test sword");
        input.EquippedWeapon = sword;
        foreach (HitscanGun gun in input.GetComponentsInChildren<HitscanGun>(true))
        {
            Undo.RecordObject(gun, "Disable gun attacks for sword testing");
            gun.enabled = false;
        }
        foreach (GunPresentation gun in camera.GetComponentsInChildren<GunPresentation>(true))
        {
            Undo.RecordObject(gun.gameObject, "Hide gun visual");
            gun.gameObject.SetActive(false);
        }
        SwordPresentation presentation = camera.GetComponentInChildren<SwordPresentation>(true);
        if (presentation == null)
        {
            Material blade = MaterialFor("SwordBlade", shader, new Color(0.7f, 0.8f, 0.9f));
            Material grip = MaterialFor("SwordGrip", shader, new Color(0.18f, 0.1f, 0.06f));
            GameObject view = Child("Sword Presentation", camera.transform);
            view.transform.localPosition = new Vector3(0.32f, -0.32f,
                Mathf.Max(0.85f, camera.nearClipPlane + 0.65f));
            GameObject model = Child("Placeholder Sword", view.transform);
            Shape("Grip", model.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.065f, 0.22f, 0.065f), grip);
            Shape("Guard", model.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.045f, 0.075f), blade);
            Shape("Blade", model.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 0.76f, 0.035f), blade);
            model.transform.localRotation = Quaternion.Euler(10f, 0f, -15f);
            presentation = Undo.AddComponent<SwordPresentation>(view);
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("sword").objectReferenceValue = sword;
            serialized.FindProperty("model").objectReferenceValue = model.transform;
            serialized.ApplyModifiedProperties();
        }
        Undo.RecordObject(presentation.gameObject, "Show sword visual");
        presentation.gameObject.SetActive(true);
        Undo.RecordObject(presentation, "Enable sword presentation");
        presentation.enabled = true;
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = sword.gameObject;
        Debug.Log("Sword test saved. This editor command equips one prototype weapon; no gameplay switching was added.");
    }

    private static GameObject Child(string name, Transform parent)
    {
        var child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create sword visual");
        SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
        child.transform.SetParent(parent, false);
        child.layer = LayerMask.NameToLayer("Ignore Raycast");
        return child;
    }

    private static void Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(shape, "Create sword shape");
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
}
