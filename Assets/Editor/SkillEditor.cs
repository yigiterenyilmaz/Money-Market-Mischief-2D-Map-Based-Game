using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Skill))]
public class SkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cost"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("prerequisites"),
            true
        );

        // Effects öncesi SP değişikliklerini uygula (reflection ile çakışmasın)
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        SkillEffectDrawer.DrawEffectList(serializedObject.FindProperty("effects"));

        // Effects sonrası reflection değişikliklerini SP'ye yansıt
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aktif Yetenek", EditorStyles.boldLabel);
        SerializedProperty ability = serializedObject.FindProperty("activeAbility");
        SerializedProperty abilityEnabled = ability.FindPropertyRelative("enabled");
        EditorGUILayout.PropertyField(abilityEnabled, new GUIContent("Aktif Yeteneği Var"));

        if (abilityEnabled.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(ability.FindPropertyRelative("abilityName"));
            EditorGUILayout.PropertyField(ability.FindPropertyRelative("description"));
            EditorGUILayout.PropertyField(ability.FindPropertyRelative("cooldownSeconds"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.LabelField("Kullanınca Çalışan Efektler", EditorStyles.miniBoldLabel);
            SkillEffectDrawer.DrawEffectList(ability.FindPropertyRelative("onActivate"));
            serializedObject.Update();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Diğer Ön Koşullar", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("otherPrerequisites")
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blocks Skills", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("blocksSkills"),
            true
        );

        serializedObject.ApplyModifiedProperties();
    }
}
