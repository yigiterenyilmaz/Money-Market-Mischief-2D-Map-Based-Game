using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Event))]
public class EventEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Event Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isRepeatable"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minGamePhase"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxGamePhase"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("requiredSkills"),
            true
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("statConditions"),
            true
        );

        EditorGUILayout.Space();
        DrawChoices();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawChoices()
    {
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

        var choicesProp = serializedObject.FindProperty("choices");

        for (int i = 0; i < choicesProp.arraySize; i++)
        {
            var choice = choicesProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Başlık ve sil butonu
            EditorGUILayout.BeginHorizontal();
            choice.isExpanded = EditorGUILayout.Foldout(choice.isExpanded, "Se\u00e7enek " + i);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                choicesProp.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (choice.isExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(choice.FindPropertyRelative("text"));

                // Effects öncesi SP değişikliklerini uygula
                serializedObject.ApplyModifiedProperties();

                // Effects - dropdown ile
                EditorGUILayout.Space(5);
                SkillEffectDrawer.DrawEffectList(choice.FindPropertyRelative("effects"));

                // Effects sonrası reflection değişikliklerini SP'ye yansıt
                serializedObject.Update();

                // Feed Override
                EditorGUILayout.Space(5);
                var overridesFeed = choice.FindPropertyRelative("overridesFeed");
                EditorGUILayout.PropertyField(overridesFeed);
                if (overridesFeed.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("feedTopic"));
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("feedOverrideRatio"));
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("feedOverrideDuration"));
                    EditorGUI.indentLevel--;
                }

                // Feed Speed Boost
                var boostsFeedSpeed = choice.FindPropertyRelative("boostsFeedSpeed");
                EditorGUILayout.PropertyField(boostsFeedSpeed);
                if (boostsFeedSpeed.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("boostedMinInterval"));
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("boostedMaxInterval"));
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("speedBoostDuration"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Se\u00e7enek Ekle"))
        {
            choicesProp.arraySize++;
        }
    }
}
