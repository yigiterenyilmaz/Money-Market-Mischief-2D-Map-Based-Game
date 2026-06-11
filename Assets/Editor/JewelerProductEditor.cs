using UnityEditor;

[CustomEditor(typeof(JewelerProduct))]
public class JewelerProductEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cost"));

        var typeProp = serializedObject.FindProperty("jewelerType");
        EditorGUILayout.PropertyField(typeProp);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("incomePer10Seconds"));

        JewelerType selectedType = (JewelerType)typeProp.enumValueIndex;

        if (selectedType == JewelerType.Illegal)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("suspicionPerMinute"));
        else
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reputationPerMinute"));

        serializedObject.ApplyModifiedProperties();
    }
}
