using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponAmmo))]
public sealed class WeaponAmmoInspector : Editor
{
    public override void OnInspectorGUI()
    {
        WeaponAmmo ammo = (WeaponAmmo)target;
        if (ammo.Definition != null)
        {
            EditorGUILayout.HelpBox("Magazine and reload tuning comes from the gun definition.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Definition", ammo.Definition, typeof(GunWeaponDefinition), false);
        }
        else
            DrawDefaultInspector();
        if (Application.isPlaying)
            EditorGUILayout.LabelField("Loaded", $"{ammo.CurrentAmmo} / {ammo.Capacity}");
    }
}
