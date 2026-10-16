using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Weapon), true)]
public sealed class WeaponInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty definition = serializedObject.FindProperty("definition");
        EditorGUILayout.PropertyField(definition);
        if (definition.objectReferenceValue != null)
        {
            EditorGUILayout.HelpBox("Weapon tuning comes from the assigned asset. Instance fallback values are ignored.", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "definition", "damage", "range", "attackCooldown",
                "attackArc", "windup", "activeDuration", "recovery");
        }
        else
            DrawPropertiesExcluding(serializedObject, "m_Script", "definition");
        serializedObject.ApplyModifiedProperties();
    }
}
