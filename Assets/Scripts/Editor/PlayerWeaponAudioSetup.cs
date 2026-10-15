using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerWeaponAudioSetup
{
    [MenuItem("Tools/ClassFPS/Set Up Player Weapon Audio")]
    public static void SetUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setting up audio.");
        Scene scene = SceneManager.GetActiveScene();
        PlayerCombatInput[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCombatInput>(true)).ToArray();
        if (players.Length != 1)
            throw new InvalidOperationException("Open the test scene with exactly one PlayerCombatInput.");
        PlayerCombatInput player = players[0];
        PlayerWeaponAudio[] existing = player.GetComponentsInChildren<PlayerWeaponAudio>(true);
        if (existing.Length > 1)
            throw new InvalidOperationException("Keep one PlayerWeaponAudio before running setup.");
        AudioClip pew = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew2.wav");
        AudioClip originalPew = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pew.wav");
        AudioClip shing = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shing.wav");
        if (pew == null || shing == null)
            throw new InvalidOperationException("Import Assets/Audio/pew2.wav and Shing.wav first.");
        HitscanGun gun = player.GetComponentInChildren<HitscanGun>(true);
        SwordWeapon sword = player.GetComponentInChildren<SwordWeapon>(true);
        if (gun == null && sword == null)
            throw new InvalidOperationException("Set up a gun or sword first.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Player Weapon Audio");
        PlayerWeaponAudio audio = existing.FirstOrDefault();
        if (audio == null)
        {
            var child = new GameObject("Player Weapon Audio");
            Undo.RegisterCreatedObjectUndo(child, "Create player weapon audio");
            SceneManager.MoveGameObjectToScene(child, scene);
            child.transform.SetParent(player.transform, false);
            audio = Undo.AddComponent<PlayerWeaponAudio>(child);
        }
        AudioSource source = audio.GetComponent<AudioSource>();
        Undo.RecordObject(source, "Configure non-spatial test audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        var serialized = new SerializedObject(audio);
        serialized.FindProperty("gun").objectReferenceValue = gun;
        serialized.FindProperty("sword").objectReferenceValue = sword;
        // Upgrade the original gun clip while preserving other custom clips/volumes.
        if (serialized.FindProperty("gunClip").objectReferenceValue == null ||
            serialized.FindProperty("gunClip").objectReferenceValue == originalPew)
            serialized.FindProperty("gunClip").objectReferenceValue = pew;
        if (serialized.FindProperty("swordClip").objectReferenceValue == null)
            serialized.FindProperty("swordClip").objectReferenceValue = shing;
        serialized.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = audio.gameObject;
        Debug.Log("Player weapon audio saved: pew2 for accepted gunshots, Shing for accepted swing starts. Training bot is unchanged and silent.");
    }
}
