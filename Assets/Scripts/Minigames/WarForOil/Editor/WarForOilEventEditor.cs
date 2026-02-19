using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(WarForOilEvent))]
public class WarForOilEventEditor : Editor
{
    //per-choice foldout durumları
    private Dictionary<int, bool> consequenceFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> prerequisiteFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> chainChoiceFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> rivalFoldouts = new Dictionary<int, bool>();
    private bool chainFoldout;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        //choices, maxRepeatCount, defaultChoiceIndex ve zincir alanları hariç tüm alanları çiz
        DrawPropertiesExcluding(serializedObject,
            "choices", "maxRepeatCount", "defaultChoiceIndex",
            "chainRole", "nextChainEvent", "chainInterval", "skillsToLock", "chainFine", "refusalThresholds");

        //isRepeatable açıksa maxRepeatCount'u göster
        SerializedProperty isRepeatable = serializedObject.FindProperty("isRepeatable");
        if (isRepeatable.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("maxRepeatCount"),
                new GUIContent("Maks Tekrar Sayısı"));
            EditorGUI.indentLevel--;
        }

        //defaultChoiceIndex — isRepeatable'dan sonra çiz
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultChoiceIndex"));

        EditorGUILayout.Space();

        //zincir ayarları
        SerializedProperty chainRole = serializedObject.FindProperty("chainRole");
        EditorGUILayout.PropertyField(chainRole, new GUIContent("Zincir Rolü"));

        ChainRole role = (ChainRole)chainRole.enumValueIndex;

        if (role == ChainRole.Head)
        {
            //head event — tüm zincir config'i + bağlantı
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("nextChainEvent"),
                new GUIContent("Sonraki Event"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("chainInterval"),
                new GUIContent("Aralık (sn)"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("skillsToLock"),
                new GUIContent("Kilitlenecek Skill"), true);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("chainFine"),
                new GUIContent("Çöküş Cezası"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("refusalThresholds"),
                new GUIContent("Ret Eşikleri"), true);

            EditorGUI.indentLevel--;
        }
        else if (role == ChainRole.Link)
        {
            //ara zincir event'i — sadece bağlantı alanları
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("nextChainEvent"),
                new GUIContent("Sonraki Event"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("chainInterval"),
                new GUIContent("Aralık (sn)"));
            EditorGUI.indentLevel--;
        }
        //None → hiçbir zincir alanı gösterilmez

        EditorGUILayout.Space();

        //choices listesi
        SerializedProperty choicesProp = serializedObject.FindProperty("choices");
        choicesProp.isExpanded = EditorGUILayout.Foldout(
            choicesProp.isExpanded, $"Choices ({choicesProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (choicesProp.isExpanded)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                if (DrawChoice(choicesProp, i))
                    break; //eleman silindiyse döngüyü kır
                EditorGUILayout.Space(4);
            }

            EditorGUI.indentLevel--;

            if (GUILayout.Button("+ Yeni Seçenek Ekle"))
            {
                int idx = choicesProp.arraySize;
                choicesProp.InsertArrayElementAtIndex(idx);
                ClearChoice(choicesProp.GetArrayElementAtIndex(idx));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Tek bir choice'u çizer. Eleman silindiyse true döner.
    /// </summary>
    private bool DrawChoice(SerializedProperty choicesProp, int index)
    {
        SerializedProperty choice = choicesProp.GetArrayElementAtIndex(index);
        string label = choice.FindPropertyRelative("displayName").stringValue;
        if (string.IsNullOrEmpty(label)) label = $"Seçenek {index}";

        EditorGUILayout.BeginHorizontal();
        choice.isExpanded = EditorGUILayout.Foldout(choice.isExpanded, label, true);
        if (GUILayout.Button("\u2212", GUILayout.Width(20))) //− (minus sign)
        {
            choicesProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            return true;
        }
        EditorGUILayout.EndHorizontal();

        if (!choice.isExpanded) return false;

        EditorGUI.indentLevel++;

        //temel alanlar
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("displayName"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("description"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("supportModifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("suspicionModifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("reputationModifier"),
            new GUIContent("İtibar Modifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("politicalInfluenceModifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("costModifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("cornerGrabModifier"),
            new GUIContent("Köşe Kapma Modifier"));
        EditorGUILayout.PropertyField(choice.FindPropertyRelative("protestModifier"),
            new GUIContent("Toplum Tepkisi Modifier"));

        EditorGUILayout.Space(4);

        //diğer sonuçlar — foldout, tiklendiğinde alt alanlar açılır
        if (!consequenceFoldouts.ContainsKey(index))
            consequenceFoldouts[index] = false;
        consequenceFoldouts[index] = EditorGUILayout.Foldout(
            consequenceFoldouts[index], "Diğer Sonuçlar", true);

        if (consequenceFoldouts[index])
        {
            EditorGUI.indentLevel++;

            SerializedProperty endsWar = choice.FindPropertyRelative("endsWar");
            EditorGUILayout.PropertyField(endsWar, new GUIContent("Savaş Bitir"));
            if (endsWar.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("warEndDelay"),
                    new GUIContent("Gecikme (sn)"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty reducesReward = choice.FindPropertyRelative("reducesReward");
            EditorGUILayout.PropertyField(reducesReward, new GUIContent("Ödül Düşür"));
            if (reducesReward.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("baseRewardReduction"),
                    new GUIContent("Oran"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty endsWarWithDeal = choice.FindPropertyRelative("endsWarWithDeal");
            EditorGUILayout.PropertyField(endsWarWithDeal, new GUIContent("Anlaşma"));
            if (endsWarWithDeal.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("dealDelay"),
                    new GUIContent("Gecikme (sn)"));
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("dealRewardRatio"),
                    new GUIContent("Ödül Oranı"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("blocksEvents"),
                new GUIContent("Event Engelle"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //zincir seçenek flagleri — foldout
        if (!chainChoiceFoldouts.ContainsKey(index))
            chainChoiceFoldouts[index] = false;
        chainChoiceFoldouts[index] = EditorGUILayout.Foldout(
            chainChoiceFoldouts[index], "Zincir Flagleri", true);

        if (chainChoiceFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("continuesChain"),
                new GUIContent("Devam Ettir"));
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("isChainRefusal"),
                new GUIContent("Reddetme"));
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("triggersCeasefire"),
                new GUIContent("Ateşkes"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //rakip işgal flagleri — foldout
        if (!rivalFoldouts.ContainsKey(index))
            rivalFoldouts[index] = false;
        rivalFoldouts[index] = EditorGUILayout.Foldout(
            rivalFoldouts[index], "Rakip İşgal Flagleri", true);

        if (rivalFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("acceptsRivalDeal"),
                new GUIContent("Anlaşmayı Kabul"));
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("rejectsRivalDeal"),
                new GUIContent("Anlaşmayı Reddet"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //ön koşullar — foldout
        if (!prerequisiteFoldouts.ContainsKey(index))
            prerequisiteFoldouts[index] = false;
        prerequisiteFoldouts[index] = EditorGUILayout.Foldout(
            prerequisiteFoldouts[index], "Ön Koşullar", true);

        if (prerequisiteFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("requiredSkills"), true);
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("statConditions"), true);
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
        return false;
    }

    private void ClearChoice(SerializedProperty choice)
    {
        choice.FindPropertyRelative("displayName").stringValue = "";
        choice.FindPropertyRelative("description").stringValue = "";
        choice.FindPropertyRelative("supportModifier").floatValue = 0f;
        choice.FindPropertyRelative("suspicionModifier").floatValue = 0f;
        choice.FindPropertyRelative("reputationModifier").floatValue = 0f;
        choice.FindPropertyRelative("politicalInfluenceModifier").floatValue = 0f;
        choice.FindPropertyRelative("costModifier").intValue = 0;
        choice.FindPropertyRelative("cornerGrabModifier").floatValue = 0f;
        choice.FindPropertyRelative("protestModifier").floatValue = 0f;
        choice.FindPropertyRelative("endsWar").boolValue = false;
        choice.FindPropertyRelative("warEndDelay").floatValue = 0f;
        choice.FindPropertyRelative("reducesReward").boolValue = false;
        choice.FindPropertyRelative("baseRewardReduction").floatValue = 0f;
        choice.FindPropertyRelative("endsWarWithDeal").boolValue = false;
        choice.FindPropertyRelative("dealDelay").floatValue = 0f;
        choice.FindPropertyRelative("dealRewardRatio").floatValue = 0f;
        choice.FindPropertyRelative("blocksEvents").boolValue = false;
        choice.FindPropertyRelative("continuesChain").boolValue = false;
        choice.FindPropertyRelative("isChainRefusal").boolValue = false;
        choice.FindPropertyRelative("triggersCeasefire").boolValue = false;
        choice.FindPropertyRelative("acceptsRivalDeal").boolValue = false;
        choice.FindPropertyRelative("rejectsRivalDeal").boolValue = false;
        choice.FindPropertyRelative("requiredSkills").ClearArray();
        choice.FindPropertyRelative("statConditions").ClearArray();
    }
}
