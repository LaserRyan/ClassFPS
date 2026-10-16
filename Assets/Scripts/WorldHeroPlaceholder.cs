using UnityEngine;

// Used by Editor setup and as a safe runtime fallback when optional art is absent.
public static class WorldHeroPlaceholder
{
    public static Color ColorFor(string id) => id == "prototype.fire" ? new Color(1f, 0.28f, 0.08f) :
        id == "prototype.sword" ? new Color(0.55f, 0.25f, 0.85f) : new Color(0.12f, 0.48f, 0.9f);

    public static WorldHeroPresentation CreateFallback(string id)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = shader != null ? new Material(shader) : null;
        if (material != null) material.SetColor("_BaseColor", ColorFor(id));
        WorldHeroPresentation result = Build(id, material, material);
        result.OwnFallbackMaterial(material);
        return result;
    }
    public static WorldHeroPresentation Build(string id, Material body, Material equipment)
    {
        var root = new GameObject("World Hero Presentation");
        Transform visuals = Child("Visuals", root.transform, Vector3.zero);
        Shape("Body", PrimitiveType.Capsule, visuals, new Vector3(0f, 0.95f, 0f),
            new Vector3(0.65f, 0.9f, 0.65f), body);
        Shape("Facing Visor (+Z)", PrimitiveType.Cube, visuals, new Vector3(0f, 1.55f, 0.3f),
            new Vector3(0.42f, 0.13f, 0.15f), equipment);
        Transform aim = Child("Aim Pitch", visuals, new Vector3(0f, 1.4f, 0f));
        Transform attack = Child("Attack Pose", aim, Vector3.zero);
        Shape("Arm", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.06f, 0.22f),
            new Vector3(0.16f, 0.16f, 0.5f), body);
        if (id == "prototype.fire")
        {
            Shape("Casting Hand", PrimitiveType.Sphere, attack, new Vector3(0.32f, -0.06f, 0.52f),
                Vector3.one * 0.23f, equipment);
        }
        else if (id == "prototype.sword")
        {
            Shape("Sword Grip", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.06f, 0.52f),
                new Vector3(0.1f, 0.1f, 0.22f), body);
            Shape("Sword Guard", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.06f, 0.67f),
                new Vector3(0.35f, 0.09f, 0.07f), equipment);
            Shape("Sword Blade", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.06f, 1.12f),
                new Vector3(0.12f, 0.05f, 0.85f), equipment);
        }
        else
        {
            Shape("Revolver Barrel", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.03f, 0.64f),
                new Vector3(0.1f, 0.1f, 0.38f), equipment);
            Shape("Revolver Cylinder", PrimitiveType.Sphere, attack, new Vector3(0.32f, -0.03f, 0.49f),
                Vector3.one * 0.19f, equipment);
            Shape("Revolver Grip", PrimitiveType.Cube, attack, new Vector3(0.32f, -0.16f, 0.46f),
                new Vector3(0.1f, 0.23f, 0.12f), body);
        }
        WorldHeroPresentation result = root.AddComponent<WorldHeroPresentation>();
        result.ConfigurePivots(visuals, aim, attack);
        return result;
    }
    private static Transform Child(string name, Transform parent, Vector3 position)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        return child.transform;
    }
    private static void Shape(string name, PrimitiveType type, Transform parent, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        // Disable immediately before deferred destruction at runtime: never enter physics.
        Collider collider = shape.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
        if (material != null) shape.GetComponent<Renderer>().sharedMaterial = material;
    }
}
