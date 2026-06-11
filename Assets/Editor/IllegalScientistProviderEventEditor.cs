using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IllegalScientistProviderEvent))]
public class IllegalScientistProviderEventEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        //ortak alanlar — her event tipinde görünür
        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eventType"));

        IllegalScientistProviderEventType eventType = (IllegalScientistProviderEventType)serializedObject.FindProperty("eventType").enumValueIndex;

        EditorGUILayout.Space();

        switch (eventType)
        {
            case IllegalScientistProviderEventType.Offer:
                EditorGUILayout.LabelField("Teklif Ayarları", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseReward"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("riskLevel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("decisionTime"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("processEvents"), true);
                break;

            case IllegalScientistProviderEventType.Process:
                EditorGUILayout.LabelField("Süreç Event Ayarları", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("decisionTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("choices"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultChoiceIndex"));
                break;

            case IllegalScientistProviderEventType.PostProcess:
                EditorGUILayout.LabelField("Musallat Event Ayarları", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("decisionTime"));
                EditorGUILayout.Space();

                //musallat etki tipi seçimi
                SerializedProperty effectProp = serializedObject.FindProperty("postProcessEffect");
                EditorGUILayout.PropertyField(effectProp);

                PostProcessEffectType effectType = (PostProcessEffectType)effectProp.enumValueIndex;

                //seçilen tipe göre ilgili alanları göster
                if (effectType == PostProcessEffectType.ScientistKill)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("scientistKillCount"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("choices"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultChoiceIndex"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
