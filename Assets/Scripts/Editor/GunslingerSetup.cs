using UnityEditor;
using UnityEngine;

public static class GunslingerSetup
{
    public static void Upgrade(HeroDefinition hero)
    {
        const string definitionPath = "Assets/Definitions/Heroes/GunslingerAbilities.asset";
        var definition = AssetDatabase.LoadAssetAtPath<GunslingerDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<GunslingerDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }
        GameObject dash = Behavior<GunslingerDash>("GunslingerDash", definition);
        GameObject passive = Behavior<GunslingerLifesteal>("GunslingerLifesteal", definition);
        var data = new SerializedObject(hero);
        if (hero.DisplayName == "Gun Prototype") data.FindProperty("displayName").stringValue = "Gunslinger";
        if (hero.HeroClass == "Ranged Prototype") data.FindProperty("heroClass").stringValue = "Revolver / Dash / Lifesteal";
        if (hero.SecondaryActionPrefab == null) data.FindProperty("secondaryActionPrefab").objectReferenceValue = dash;
        if (hero.PassivePrefab == null) data.FindProperty("passivePrefab").objectReferenceValue = passive;
        data.ApplyModifiedPropertiesWithoutUndo();
        UpgradeRevolver(AssetDatabase.GetAssetPath(hero.KitPrefab));
    }

    private static GameObject Behavior<T>(string name, GunslingerDefinition definition) where T : Component
    {
        string path = "Assets/Prefabs/Heroes/" + name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        try
        {
            T component = root.GetComponent<T>();
            if (component == null) component = root.AddComponent<T>();
            var data = new SerializedObject(component);
            if (data.FindProperty("definition").objectReferenceValue == null)
                data.FindProperty("definition").objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (component is GunslingerDash)
            {
                if (root.GetComponent<GunslingerDashHUD>() == null) root.AddComponent<GunslingerDashHUD>();
                if (root.GetComponent<DashPresentation>() == null)
                {
                    root.AddComponent<DashPresentation>();
                    AudioSource source = root.GetComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                }
            }
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static void UpgradeRevolver(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GunPresentation view = root.GetComponentInChildren<GunPresentation>(true);
            Transform model = view != null ? view.transform.Find("Placeholder Model") : null;
            if (model == null || model.Find("Revolver Cylinder") != null) return;
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Revolver Cylinder";
            cylinder.layer = LayerMask.NameToLayer("Ignore Raycast");
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.transform.SetParent(model, false);
            cylinder.transform.localPosition = new Vector3(0f, 0.015f, 0.12f);
            cylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cylinder.transform.localScale = new Vector3(0.19f, 0.08f, 0.19f);
            Renderer existing = model.GetComponentInChildren<Renderer>();
            if (existing != null) cylinder.GetComponent<Renderer>().sharedMaterial = existing.sharedMaterial;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
