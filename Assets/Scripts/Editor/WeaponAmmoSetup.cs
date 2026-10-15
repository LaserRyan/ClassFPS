using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WeaponAmmoSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Weapon Ammo HUD")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up ammo.");
        Scene scene = SceneManager.GetActiveScene();
        PlayerCombatInput[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCombatInput>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Open the test scene with exactly one PlayerCombatInput.");
        PlayerCombatInput player = players[0];
        WeaponAmmoHUD[] huds = player.GetComponentsInChildren<WeaponAmmoHUD>(true);
        if (huds.Length > 1)
            throw new InvalidOperationException("Keep one WeaponAmmoHUD before running setup.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Weapon Ammo HUD");
        foreach (HitscanGun gun in player.GetComponentsInChildren<HitscanGun>(true))
            if (gun.GetComponent<WeaponAmmo>() == null)
                Undo.AddComponent<WeaponAmmo>(gun.gameObject);
        WeaponAmmoHUD hud = huds.FirstOrDefault();
        if (hud == null)
        {
            var view = new GameObject("Weapon Ammo HUD");
            Undo.RegisterCreatedObjectUndo(view, "Create ammo HUD");
            SceneManager.MoveGameObjectToScene(view, scene);
            view.transform.SetParent(player.transform, false);
            hud = Undo.AddComponent<WeaponAmmoHUD>(view);
        }
        Undo.RecordObject(hud, "Enable ammo HUD");
        hud.enabled = true;
        Undo.RecordObject(hud.gameObject, "Show ammo HUD");
        hud.gameObject.SetActive(true);
        var serialized = new SerializedObject(hud);
        serialized.FindProperty("combat").objectReferenceValue = player;
        serialized.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Ammo HUD saved. Gun magazine/reload settings are on WeaponAmmo; sword shows infinity. R manually reloads; empty magazines auto-reload.");
    }
}
