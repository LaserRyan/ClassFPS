using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerHealthSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Player Health HUD")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up health.");
        Scene scene = SceneManager.GetActiveScene();
        FPSController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<FPSController>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Open MovementTest with exactly one FPSController.");
        FPSController player = players[0];
        PlayerHealthHUD[] huds = player.GetComponentsInChildren<PlayerHealthHUD>(true);
        if (huds.Length > 1)
            throw new InvalidOperationException("Player has multiple health HUDs; keep one before running setup.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Player Health HUD");
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
            health = Undo.AddComponent<PlayerHealth>(player.gameObject);
        Undo.RecordObject(health, "Enable player health");
        health.enabled = true;
        PlayerHealthDebugInput debug = player.GetComponent<PlayerHealthDebugInput>();
        if (debug == null)
            debug = Undo.AddComponent<PlayerHealthDebugInput>(player.gameObject);
        Undo.RecordObject(debug, "Enable development health controls");
        debug.enabled = true;

        PlayerHealthHUD hud = huds.FirstOrDefault();
        if (hud == null)
        {
            var view = new GameObject("Player Health HUD");
            Undo.RegisterCreatedObjectUndo(view, "Create health HUD");
            SceneManager.MoveGameObjectToScene(view, scene);
            view.transform.SetParent(player.transform, false);
            hud = Undo.AddComponent<PlayerHealthHUD>(view);
        }
        Undo.RecordObject(hud.gameObject, "Show health HUD");
        hud.gameObject.SetActive(true);
        Undo.RecordObject(hud, "Bind health HUD");
        hud.enabled = true;
        var serialized = new SerializedObject(hud);
        serialized.FindProperty("health").objectReferenceValue = health;
        serialized.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Player health HUD saved. Editor/development only: F8 deals test damage; F9 restores full health while gameplay input is captured.");
    }
}
