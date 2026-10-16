using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Called by the existing hero setup after its folders are created.
public static class FireSpellPrototypeSetup
{
    private const string Folder = "Assets/Prefabs/Heroes/";
    public static HeroKit CreateKit()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            throw new InvalidOperationException("URP Unlit is required for the fire prototype.");
        Material fire = MaterialFor("PrototypeFire", shader, new Color(1f, 0.22f, 0.02f));
        Material trail = MaterialFor("PrototypeFireTrail", shader, new Color(1f, 0.65f, 0.05f));
        FireImpactPresentation impact = ImpactPrefab(fire);
        StraightProjectile projectile = ProjectilePrefab(fire, trail, impact);
        const string definitionPath = "Assets/Definitions/Weapons/FireSpellPrototype.asset";
        FireSpellDefinition definition = AssetDatabase.LoadAssetAtPath<FireSpellDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<FireSpellDefinition>();
            var data = new SerializedObject(definition);
            data.FindProperty("damage").floatValue = 35f;
            data.FindProperty("range").floatValue = 80f;
            data.FindProperty("cooldown").floatValue = 0.6f;
            data.FindProperty("projectilePrefab").objectReferenceValue = projectile;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }
        string path = Folder + "FireSpellPrototypeKit.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<HeroKit>();
        var root = new GameObject("Fire Spell Prototype Kit");
        try
        {
            FireSpellWeapon weapon = root.AddComponent<FireSpellWeapon>();
            var view = new GameObject("Fire Spell Presentation");
            view.transform.SetParent(root.transform, false);
            view.transform.localPosition = new Vector3(0.28f, -0.2f, 0.65f);
            GameObject charge = Sphere("Charge", view.transform, fire);
            charge.transform.localScale = Vector3.one * 0.05f;
            charge.SetActive(false);
            FireSpellPresentation presentation = view.AddComponent<FireSpellPresentation>();
            ConfigureSource(view.GetComponent<AudioSource>());
            var presentationData = new SerializedObject(presentation);
            presentationData.FindProperty("weapon").objectReferenceValue = weapon;
            presentationData.FindProperty("chargeVisual").objectReferenceValue = charge.transform;
            presentationData.ApplyModifiedPropertiesWithoutUndo();
            var weaponData = new SerializedObject(weapon);
            weaponData.FindProperty("definition").objectReferenceValue = definition;
            weaponData.FindProperty("castOrigin").objectReferenceValue = view.transform;
            weaponData.ApplyModifiedPropertiesWithoutUndo();
            HeroKit kit = root.AddComponent<HeroKit>();
            var kitData = new SerializedObject(kit);
            kitData.FindProperty("primaryWeapon").objectReferenceValue = weapon;
            kitData.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<HeroKit>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static FireImpactPresentation ImpactPrefab(Material material)
    {
        string path = Folder + "FireImpact.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<FireImpactPresentation>();
        var root = new GameObject("Fire Impact");
        try
        {
            GameObject visual = Sphere("Impact Pulse", root.transform, material);
            FireImpactPresentation impact = root.AddComponent<FireImpactPresentation>();
            ConfigureSource(root.GetComponent<AudioSource>());
            var data = new SerializedObject(impact);
            data.FindProperty("visual").objectReferenceValue = visual.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<FireImpactPresentation>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static StraightProjectile ProjectilePrefab(Material bodyMaterial, Material trailMaterial,
        FireImpactPresentation impact)
    {
        string path = Folder + "Fireball.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing.GetComponent<StraightProjectile>();
        var root = new GameObject("Fireball");
        try
        {
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            GameObject visual = Sphere("Fireball Body", root.transform, bodyMaterial);
            visual.transform.localScale = Vector3.one * 0.4f;
            var trailObject = new GameObject("Fire Trail");
            trailObject.transform.SetParent(root.transform, false);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.22f;
            trail.minVertexDistance = 0.06f;
            trail.startWidth = 0.32f;
            trail.endWidth = 0f;
            trail.startColor = Color.yellow;
            trail.endColor = new Color(1f, 0.15f, 0f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            StraightProjectile projectile = root.AddComponent<StraightProjectile>();
            var projectileData = new SerializedObject(projectile);
            projectileData.FindProperty("collisionRadius").floatValue = 0.2f;
            projectileData.ApplyModifiedPropertiesWithoutUndo();
            FireballPresentation presentation = root.AddComponent<FireballPresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("visual").objectReferenceValue = visual.transform;
            data.FindProperty("trail").objectReferenceValue = trail;
            data.FindProperty("impactPrefab").objectReferenceValue = impact;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<StraightProjectile>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    private static GameObject Sphere(string name, Transform parent, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.layer = LayerMask.NameToLayer("Ignore Raycast");
        UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return sphere;
    }

    private static Material MaterialFor(string name, Shader shader, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
