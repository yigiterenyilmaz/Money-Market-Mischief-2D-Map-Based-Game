using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SocialMediaPost))]
public class SocialMediaPostEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("authorName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("content"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topic"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("authorAvatar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isRepeatable"));

        serializedObject.ApplyModifiedProperties();
    }
}
