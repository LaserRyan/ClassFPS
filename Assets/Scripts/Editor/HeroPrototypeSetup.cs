using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HeroPrototypeSetup
{
    private const string HeroFolder = "Assets/Definitions/Heroes";
    private const string WeaponFolder = "Assets/Definitions/Weapons";
    private const string KitFolder = "Assets/Prefabs/Heroes";

    static HeroPrototypeSetup()
    {
        EditorApplication.delayCall += MigrateOriginalGunshot;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += MigrateOriginalGunshot;
        };
    }

    private static void MigrateOriginalGunshot()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        UpgradeGunAudio(KitFolder + "/GunPrototypeKit.prefab");
        UpgradeGunAudio(KitFolder + "/SwordPrototypeKit.prefab");
    }

    [MenuItem("Tools/ClassFPS/Set Up Prototype Hero Selection")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before hero setup.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/MovementTest.unity")
            throw new InvalidOperationException("Open Assets/MovementTest.unity first.");
        PlayerCombatInput[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCombatInput>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Hero setup requires exactly one shared player.");
        PlayerCombatInput player = players[0];
        FPSController movement = player.GetComponent<FPSController>();
        if (movement == null || movement.PlayerCamera == null || player.GetComponent<PlayerLifecycle>() == null)
            throw new InvalidOperationException("Set up the existing player health/respawn training test first.");
        Folder(HeroFolder);
        Folder(WeaponFolder);
        Folder(KitFolder);
        HitscanGun gun = player.GetComponentInChildren<HitscanGun>(true);
        SwordWeapon sword = player.GetComponentInChildren<SwordWeapon>(true);
        GunPresentation gunView = player.GetComponentInChildren<GunPresentation>(true);
        SwordPresentation swordView = player.GetComponentInChildren<SwordPresentation>(true);
        PlayerWeaponAudio audio = player.GetComponentInChildren<PlayerWeaponAudio>(true);
        GunWeaponDefinition gunDefinition = Definition<GunWeaponDefinition>("GunPrototype", gun);
        SwordWeaponDefinition swordDefinition = Definition<SwordWeaponDefinition>("SwordPrototype", sword);
        HeroKit gunKit = Kit("GunPrototypeKit", gun, gunView, audio, gunDefinition);
        HeroKit swordKit = Kit("SwordPrototypeKit", sword, swordView, audio, swordDefinition);
        var movementData = new SerializedObject(movement);
        float speed = movementData.FindProperty("movementSpeed").floatValue;
        float jump = movementData.FindProperty("jumpHeight").floatValue;
        float gravity = movementData.FindProperty("gravity").floatValue;
        float maximum = player.GetComponent<PlayerHealth>().MaximumHealth;
        HeroDefinition gunHero = Hero("GunPrototype", "prototype.gun", "Gun Prototype", "Ranged Prototype",
            maximum, speed, jump, gravity, gunKit);
        HeroDefinition swordHero = Hero("SwordPrototype", "prototype.sword", "Sword Prototype", "Melee Prototype",
            maximum + 50f, speed + 1f, jump + 0.4f, gravity, swordKit);
        HeroKit fireKit = FireSpellPrototypeSetup.CreateKit();
        HeroDefinition fireHero = Hero("FireSpellPrototype", "prototype.fire", "Fire Spell Prototype", "Caster Prototype",
            110f, 5.25f, 1.2f, -20f, fireKit);
        Validate(gunHero, swordHero, fireHero);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Prototype Hero Selection");
        // Kits have been saved before removing legacy scene-only weapon instances.
        Undo.RecordObject(player, "Unequip legacy weapon");
        player.EquippedWeapon = null;
        foreach (GunPresentation view in player.GetComponentsInChildren<GunPresentation>(true))
            Undo.DestroyObjectImmediate(view.gameObject);
        foreach (SwordPresentation view in player.GetComponentsInChildren<SwordPresentation>(true))
            Undo.DestroyObjectImmediate(view.gameObject);
        foreach (FireSpellPresentation view in player.GetComponentsInChildren<FireSpellPresentation>(true))
            Undo.DestroyObjectImmediate(view.gameObject);
        foreach (PlayerWeaponAudio sound in player.GetComponentsInChildren<PlayerWeaponAudio>(true))
        {
            if (sound.gameObject != player.gameObject)
                Undo.DestroyObjectImmediate(sound.gameObject);
            else
                Undo.DestroyObjectImmediate(sound);
        }
        GameObject[] oldWeaponObjects = player.GetComponentsInChildren<Weapon>(true)
            .Select(weapon => weapon.gameObject).Distinct().ToArray();
        foreach (Weapon weapon in player.GetComponentsInChildren<Weapon>(true))
            Undo.DestroyObjectImmediate(weapon);
        foreach (WeaponAmmo ammo in player.GetComponentsInChildren<WeaponAmmo>(true))
            Undo.DestroyObjectImmediate(ammo);
        foreach (GameObject old in oldWeaponObjects)
            if (old != player.gameObject && old.transform.childCount == 0 &&
                old.GetComponents<Component>().Length == 1)
                Undo.DestroyObjectImmediate(old);
        HeroSelection selection = player.GetComponent<HeroSelection>();
        if (selection == null)
            selection = Undo.AddComponent<HeroSelection>(player.gameObject);
        Undo.RecordObject(selection, "Enable hero selection");
        selection.enabled = true;
        var data = new SerializedObject(selection);
        SerializedProperty heroes = data.FindProperty("heroes");
        heroes.arraySize = 3;
        heroes.GetArrayElementAtIndex(0).objectReferenceValue = gunHero;
        heroes.GetArrayElementAtIndex(1).objectReferenceValue = swordHero;
        heroes.GetArrayElementAtIndex(2).objectReferenceValue = fireHero;
        data.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Prototype hero selection saved. Only the chosen kit is instantiated in Play mode. Edit hero/weapon assets under Assets/Definitions.");
    }

    public static void RequireLegacyMode(GameObject player)
    {
        if (player.GetComponent<HeroSelection>() != null)
            throw new InvalidOperationException("Hero selection owns the loadout. Edit the hero kit/assets, or use Tools > ClassFPS > Use Legacy Gun Test to explicitly leave hero mode.");
    }

    [MenuItem("Tools/ClassFPS/Use Legacy Gun Test")]
    public static void UseLegacyGunTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode first.");
        if (SceneManager.GetActiveScene().path != "Assets/MovementTest.unity")
            throw new InvalidOperationException("Open Assets/MovementTest.unity first.");
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (HeroSelection selection in root.GetComponentsInChildren<HeroSelection>(true))
                Undo.DestroyObjectImmediate(selection);
        ShootingTestSetup.SetUp();
        GunPresentationSetup.SetUp();
        PlayerWeaponAudioSetup.SetUp();
        WeaponAmmoSetup.SetUp();
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    [MenuItem("Tools/ClassFPS/Validate Prototype Hero Assets")]
    public static void ValidateAssets()
    {
        HeroDefinition gun = AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroFolder + "/GunPrototype.asset");
        HeroDefinition sword = AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroFolder + "/SwordPrototype.asset");
        HeroDefinition fire = AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroFolder + "/FireSpellPrototype.asset");
        if (gun == null || sword == null || fire == null)
            throw new InvalidOperationException("Run Set Up Prototype Hero Selection first.");
        Validate(gun, sword, fire);
        Debug.Log("Prototype hero assets passed ID and kit/definition validation. This is not a gameplay test.");
    }

    private static T Definition<T>(string name, Weapon source) where T : WeaponDefinition
    {
        string path = WeaponFolder + "/" + name + ".asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;
        T asset = ScriptableObject.CreateInstance<T>();
        var data = new SerializedObject(asset);
        bool melee = asset is SwordWeaponDefinition;
        data.FindProperty("damage").floatValue = source != null ? source.Damage : melee ? 35f : 25f;
        data.FindProperty("range").floatValue = source != null ? source.Range : melee ? 2.2f : 100f;
        data.FindProperty("cooldown").floatValue = source != null ? source.Cooldown : melee ? 0f : 0.2f;
        if (source is SwordWeapon blade)
        {
            data.FindProperty("attackArc").floatValue = blade.AttackArc;
            data.FindProperty("windup").floatValue = blade.Windup;
            data.FindProperty("activeDuration").floatValue = blade.ActiveDuration;
            data.FindProperty("recovery").floatValue = blade.Recovery;
        }
        if (asset is GunWeaponDefinition && source != null && source.Ammo != null)
        {
            data.FindProperty("magazineCapacity").intValue = source.Ammo.Capacity;
            data.FindProperty("reloadDuration").floatValue = source.Ammo.ReloadDuration;
            data.FindProperty("automaticReload").boolValue = source.Ammo.AutomaticReload;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static HeroDefinition Hero(string name, string id, string display, string heroClass,
        float health, float speed, float jump, float gravity, HeroKit kit)
    {
        string path = HeroFolder + "/" + name + ".asset";
        HeroDefinition existing = AssetDatabase.LoadAssetAtPath<HeroDefinition>(path);
        if (existing != null)
            return existing;
        HeroDefinition asset = ScriptableObject.CreateInstance<HeroDefinition>();
        var data = new SerializedObject(asset);
        data.FindProperty("heroId").stringValue = id;
        data.FindProperty("displayName").stringValue = display;
        data.FindProperty("heroClass").stringValue = heroClass;
        data.FindProperty("maximumHealth").floatValue = health;
        data.FindProperty("movementSpeed").floatValue = speed;
        data.FindProperty("jumpHeight").floatValue = jump;
        data.FindProperty("gravity").floatValue = gravity;
        data.FindProperty("kitPrefab").objectReferenceValue = kit;
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static HeroKit Kit(string name, Weapon source, Component presentation,
        PlayerWeaponAudio audioSource, WeaponDefinition definition)
    {
        string path = KitFolder + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            HeroKit result = existing.GetComponent<HeroKit>();
            if (result == null)
                throw new InvalidOperationException(path + " needs HeroKit.");
            UpgradeGunAudio(path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HeroKit>();
        }
        var temporary = new GameObject(name);
        try
        {
            Weapon weapon = definition is GunWeaponDefinition ?
                (Weapon)temporary.AddComponent<HitscanGun>() : temporary.AddComponent<SwordWeapon>();
            if (source != null)
                EditorUtility.CopySerialized(source, weapon);
            weapon.enabled = true;
            var weaponData = new SerializedObject(weapon);
            weaponData.FindProperty("definition").objectReferenceValue = definition;
            weaponData.ApplyModifiedPropertiesWithoutUndo();
            if (presentation != null)
            {
                GameObject view = UnityEngine.Object.Instantiate(presentation.gameObject, temporary.transform, false);
                view.name = presentation.gameObject.name;
                view.SetActive(true);
                Component behavior = view.GetComponent(presentation.GetType());
                var viewData = new SerializedObject(behavior);
                viewData.FindProperty(weapon is HitscanGun ? "gun" : "sword").objectReferenceValue = weapon;
                viewData.FindProperty("m_Enabled").boolValue = true;
                viewData.ApplyModifiedPropertiesWithoutUndo();
            }
            var sound = new GameObject("Player Weapon Audio");
            sound.transform.SetParent(temporary.transform, false);
            PlayerWeaponAudio soundComponent = sound.AddComponent<PlayerWeaponAudio>();
            AudioSource sourceAudio = sound.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                EditorUtility.CopySerialized(audioSource, soundComponent);
                EditorUtility.CopySerialized(audioSource.GetComponent<AudioSource>(), sourceAudio);
            }
            sourceAudio.playOnAwake = false;
            sourceAudio.loop = false;
            sourceAudio.spatialBlend = 0f;
            var soundData = new SerializedObject(soundComponent);
            soundData.FindProperty("gun").objectReferenceValue = weapon as HitscanGun;
            soundData.FindProperty("sword").objectReferenceValue = weapon as SwordWeapon;
            if (soundData.FindProperty("gunClip").objectReferenceValue == null ||
                soundData.FindProperty("gunClip").objectReferenceValue ==
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew.wav"))
                soundData.FindProperty("gunClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew2.wav");
            if (soundData.FindProperty("swordClip").objectReferenceValue == null)
                soundData.FindProperty("swordClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shing.wav");
            soundData.ApplyModifiedPropertiesWithoutUndo();
            HeroKit kit = temporary.AddComponent<HeroKit>();
            var kitData = new SerializedObject(kit);
            kitData.FindProperty("primaryWeapon").objectReferenceValue = weapon;
            kitData.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(temporary, path).GetComponent<HeroKit>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }

    private static void Validate(params HeroDefinition[] heroes)
    {
        if (heroes.Select(hero => hero.HeroId).Distinct().Count() != heroes.Length)
            throw new InvalidOperationException("Prototype hero IDs must be unique.");
        foreach (HeroDefinition hero in heroes)
            if (!HeroSelection.IsValid(hero, out string error))
                throw new InvalidOperationException(hero.name + ": " + error);
    }

    [MenuItem("Tools/ClassFPS/Update Prototype Gunshot to Pew2")]
    public static void UpdatePrototypeGunshot()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before updating kit audio.");
        if (AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew2.wav") == null)
            throw new InvalidOperationException("Import Assets/Audio/pew2.wav first.");
        UpgradeGunAudio(KitFolder + "/GunPrototypeKit.prefab", true);
        UpgradeGunAudio(KitFolder + "/SwordPrototypeKit.prefab", true);
        AssetDatabase.SaveAssets();
        Debug.Log("Prototype kit gunshot clips explicitly assigned to pew2. Sword sound preserved.");
    }

    private static void UpgradeGunAudio(string path, bool force = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return;
        AudioClip pew2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew2.wav");
        if (pew2 == null)
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            foreach (PlayerWeaponAudio audio in root.GetComponentsInChildren<PlayerWeaponAudio>(true))
            {
                var data = new SerializedObject(audio);
                SerializedProperty clip = data.FindProperty("gunClip");
                if (clip.objectReferenceValue == pew2)
                    continue;
                if (!force && clip.objectReferenceValue != null &&
                    AssetDatabase.GetAssetPath(clip.objectReferenceValue) != "Assets/Audio/pew.wav")
                    continue;
                clip.objectReferenceValue = pew2;
                data.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (PlayerWeaponAudio audio in saved.GetComponentsInChildren<PlayerWeaponAudio>(true))
                    if (new SerializedObject(audio).FindProperty("gunClip").objectReferenceValue != pew2)
                        throw new InvalidOperationException("Failed to save pew2 gunshot on " + path);
                Debug.Log("Verified pew2 gunshot reference saved: " + path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
