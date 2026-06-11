using UnityEditor;

[CustomEditor(typeof(InvestmentProduct))]
public class InvestmentProductEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("productType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cost"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("profitChance"));
        var isStreakBreaker = serializedObject.FindProperty("isStreakBreakerActive");
        EditorGUILayout.PropertyField(isStreakBreaker);
        if (isStreakBreaker.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("streakBreakerType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("streakBreakerMin"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("streakBreakerMax"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxProfitPercent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxLossPercent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minReachTime"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxReachTime"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volatility"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("postPotentialDrift"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("postPotentialTimeout"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Piyasa Salınımı (satın alınmadan önceki idle hareket)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("idleOscillationMin"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("idleOscillationMax"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Piyasa Erişilebilirliği", EditorStyles.boldLabel);
        var hasLimited = serializedObject.FindProperty("hasLimitedAvailability");
        EditorGUILayout.PropertyField(hasLimited);
        if (hasLimited.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("availabilityChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("availabilityCycleDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minAvailableDuration"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
