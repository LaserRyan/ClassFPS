using System;
using UnityEditor;
using UnityEngine;

public static class WorldHeroPresentationSetup
{
    public static void Upgrade(HeroDefinition hero, string name)
    {
        // Explicitly assigned art/configuration is owned by the user.
        if (hero.WorldPresentationPrefab != null) return;
        string path = "Assets/Prefabs/Heroes/" + name + "World.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        WorldHeroPresentation prefab = existing != null ? existing.GetComponent<WorldHeroPresentation>() : null;
        if (existing != null && prefab == null)
            throw new InvalidOperationException(path + " needs WorldHeroPresentation; existing art was left intact.");
        if (prefab == null)
        {
            Material body = MaterialFor(name + "WorldBody", WorldHeroPlaceholder.ColorFor(hero.HeroId));
            Material trim = MaterialFor("WorldEquipment", new Color(0.75f, 0.82f, 0.86f));
            WorldHeroPresentation temporary = WorldHeroPlaceholder.Build(hero.HeroId, body, trim);
            try { prefab = PrefabUtility.SaveAsPrefabAsset(temporary.gameObject, path).GetComponent<WorldHeroPresentation>(); }
            finally { UnityEngine.Object.DestroyImmediate(temporary.gameObject); }
        }
        var data = new SerializedObject(hero);
        data.FindProperty("worldPresentationPrefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Material MaterialFor(string name, Color color)
    {
        const string folder = "Assets/Materials/WorldHeroes";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "WorldHeroes");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is required for world placeholders.");
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

}
