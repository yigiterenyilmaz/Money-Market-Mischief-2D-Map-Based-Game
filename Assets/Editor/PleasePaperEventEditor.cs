using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PleasePaperEvent))]
public class PleasePaperEventEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        //ortak alanlar — her event tipinde görünür
        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

        EditorGUILayout.Space();
        var eventTypeProp = serializedObject.FindProperty("eventType");
        EditorGUILayout.PropertyField(eventTypeProp);

        PleasePaperEventType eventType = (PleasePaperEventType)eventTypeProp.enumValueIndex;

        EditorGUILayout.Space();

        switch (eventType)
        {
            case PleasePaperEventType.Offer:
                DrawOfferFields();
                break;
            case PleasePaperEventType.FakeCrisis:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("decisionTime"),
                    new GUIContent("Karar Süresi (s)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultChoiceIndex"),
                    new GUIContent("Süre Dolunca Seçilecek", "Süre dolunca otomatik seçilecek seçenek indexi (0'dan başlar, -1 = ilk seçenek)"));
                EditorGUILayout.Space();
                DrawChoices();
                break;
            case PleasePaperEventType.Process:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("decisionTime"),
                    new GUIContent("Karar Süresi (s)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultChoiceIndex"),
                    new GUIContent("Süre Dolunca Seçilecek", "Süre dolunca otomatik seçilecek seçenek indexi (0'dan başlar, -1 = ilk seçenek)"));
                EditorGUILayout.Space();
                DrawChoices();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Offer tipine özel alanlar: baseReward, isFakeCrisis, fakeCrisisEvents
    /// </summary>
    private void DrawOfferFields()
    {
        EditorGUILayout.LabelField("Teklif Ayarları", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseReward"));

        var isFakeCrisisProp = serializedObject.FindProperty("isFakeCrisis");
        EditorGUILayout.PropertyField(isFakeCrisisProp);

        if (isFakeCrisisProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("realChanceOnRepeat"),
                new GUIContent("Tekrar Gelince Gerçek Olma Şansı",
                    "Sahte kriz ortaya çıktıktan sonra tekrar geldiğinde gerçek olma olasılığı (0 = hep fake, 1 = hep gerçek)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("fakeCrisisEvents"),
                new GUIContent("Sahte Kriz Event Zinciri"),
                true
            );
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("processEvents"),
            new GUIContent("Süreç Event Havuzu",
                "Bu offer gerçek kriz olarak çalıştığında kullanılacak event havuzu. Boşsa database.processEvents kullanılır."),
            true
        );
    }

    /// <summary>
    /// FakeCrisis ve Process tipleri için seçenek listesi
    /// </summary>
    private void DrawChoices()
    {
        EditorGUILayout.LabelField("Seçenekler", EditorStyles.boldLabel);

        var choicesProp = serializedObject.FindProperty("choices");

        for (int i = 0; i < choicesProp.arraySize; i++)
        {
            var choice = choicesProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            //başlık ve sil butonu
            EditorGUILayout.BeginHorizontal();
            choice.isExpanded = EditorGUILayout.Foldout(choice.isExpanded, "Seçenek " + i);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                choicesProp.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (choice.isExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(choice.FindPropertyRelative("displayName"));
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("description"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Modifier'lar", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("controlStatModifier"));
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("suspicionModifier"));
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("costModifier"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Seçenek Ekle"))
        {
            choicesProp.arraySize++;
        }
    }
}
