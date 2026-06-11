using UnityEditor;

[CustomEditor(typeof(PassiveIncomeProduct))]
public class PassiveIncomeProductEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cost"));

        var isSellable = serializedObject.FindProperty("isSellable");
        EditorGUILayout.PropertyField(isSellable);
        if (isSellable.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sellRatio"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("minIncomePerSecond"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxIncomePerSecond"));

        serializedObject.ApplyModifiedProperties();
    }
}
