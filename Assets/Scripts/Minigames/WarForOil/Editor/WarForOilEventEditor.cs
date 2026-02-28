using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(WarForOilEvent))]
public class WarForOilEventEditor : Editor
{
    //per-choice foldout durumları
    private Dictionary<int, bool> modifierFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> consequenceFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> prerequisiteFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> chainChoiceFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> rivalFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> protestFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> vandalismFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> mediaPursuitFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> feedFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> rewardFoldouts = new Dictionary<int, bool>();
    private bool chainFoldout;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        //choices, repeat alanları, defaultChoiceIndex ve zincir alanları hariç tüm alanları çiz
        DrawPropertiesExcluding(serializedObject,
            "choices", "isUnlimitedRepeat", "maxRepeatCount", "defaultChoiceIndex",
            "isVandalismEvent", "vandalismLevelOnTrigger",
            "isMediaPursuitEvent", "mediaPursuitLevelOnTrigger",
            "chainRole", "nextChainEvent", "chainInterval", "skillsToLock", "chainFine", "refusalThresholds");

        //isRepeatable açıksa tekrar seçeneklerini göster
        SerializedProperty isRepeatable = serializedObject.FindProperty("isRepeatable");
        if (isRepeatable.boolValue)
        {
            EditorGUI.indentLevel++;

            SerializedProperty isUnlimited = serializedObject.FindProperty("isUnlimitedRepeat");
            EditorGUILayout.PropertyField(isUnlimited, new GUIContent("Sınırsız Tekrar"));

            if (!isUnlimited.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("maxRepeatCount"),
                    new GUIContent("Maks Tekrar Sayısı"));
            }

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

        //vandalizm tetikleme
        SerializedProperty isVandalismEvent = serializedObject.FindProperty("isVandalismEvent");
        EditorGUILayout.PropertyField(isVandalismEvent, new GUIContent("Vandalizm Eventi"));
        if (isVandalismEvent.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("vandalismLevelOnTrigger"),
                new GUIContent("Tetiklenince Seviye"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        //medya takibi tetikleme
        SerializedProperty isMediaPursuitEvent = serializedObject.FindProperty("isMediaPursuitEvent");
        EditorGUILayout.PropertyField(isMediaPursuitEvent, new GUIContent("Medya Takibi Eventi"));
        if (isMediaPursuitEvent.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("mediaPursuitLevelOnTrigger"),
                new GUIContent("Tetiklenince Seviye"));
            EditorGUI.indentLevel--;
        }

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

        EditorGUILayout.Space(2);

        //modifier'lar — foldout
        if (!modifierFoldouts.ContainsKey(index))
            modifierFoldouts[index] = false;
        modifierFoldouts[index] = EditorGUILayout.Foldout(
            modifierFoldouts[index], "Modifiers", true);

        if (modifierFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("supportModifier"),
                new GUIContent("War Support"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("suspicionModifier"),
                new GUIContent("Şüphe"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("reputationModifier"),
                new GUIContent("İtibar"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("politicalInfluenceModifier"),
                new GUIContent("Politik Nüfuz"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("costModifier"),
                new GUIContent("Maliyet (Birikimli)"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("wealthModifier"),
                new GUIContent("Anlık Para"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("cornerGrabModifier"),
                new GUIContent("Köşe Kapma"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //protest etkisi — foldout
        if (!protestFoldouts.ContainsKey(index))
            protestFoldouts[index] = false;
        protestFoldouts[index] = EditorGUILayout.Foldout(
            protestFoldouts[index], "Protest Etkisi", true);

        if (protestFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("protestModifier"),
                new GUIContent("Toplum Tepkisi"));
            SerializedProperty protestBonus = choice.FindPropertyRelative("protestTriggerChanceBonus");
            protestBonus.isExpanded = EditorGUILayout.Foldout(protestBonus.isExpanded, "Protest Tetikleme Bonusu", true);
            if (protestBonus.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Slider(protestBonus, 0f, 1f, GUIContent.none);
                EditorGUI.indentLevel--;
            }

            SerializedProperty hasProtestChance = choice.FindPropertyRelative("hasProtestChance");
            EditorGUILayout.PropertyField(hasProtestChance, new GUIContent("Olasılıklı Tepki"));
            if (hasProtestChance.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("protestDecreaseChance"),
                    new GUIContent("Azalma İhtimali"));
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("protestDecreaseAmount"),
                    new GUIContent("Azalma Miktarı"));
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("protestIncreaseAmount"),
                    new GUIContent("Artma Miktarı"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

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

            //ödül düşür — alt foldout
            if (!rewardFoldouts.ContainsKey(index))
                rewardFoldouts[index] = false;
            rewardFoldouts[index] = EditorGUILayout.Foldout(
                rewardFoldouts[index], "Ödül Düşür", true);

            if (rewardFoldouts[index])
            {
                EditorGUI.indentLevel++;

                //direkt ödül düşürme
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

                //olasılıklı ödül düşürme
                SerializedProperty hasProbReward = choice.FindPropertyRelative("hasProbabilisticRewardReduction");
                EditorGUILayout.PropertyField(hasProbReward, new GUIContent("Olasılıklı Ödül Düşür"));
                if (hasProbReward.boolValue)
                {
                    EditorGUI.indentLevel++;

                    SerializedProperty probRetrigger = choice.FindPropertyRelative("probRetriggerChance");
                    SerializedProperty probReduction = choice.FindPropertyRelative("probRewardReductionChance");

                    //tekrar tetiklenme şansı
                    probRetrigger.isExpanded = EditorGUILayout.Foldout(probRetrigger.isExpanded, "Tekrar Tetiklenme Şansı", true);
                    if (probRetrigger.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Slider(probRetrigger, 0f, 1f, GUIContent.none);
                        EditorGUI.indentLevel--;
                    }

                    //ödül düşme şansı
                    probReduction.isExpanded = EditorGUILayout.Foldout(probReduction.isExpanded, "Ödül Düşme Şansı", true);
                    if (probReduction.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Slider(probReduction, 0f, 1f, GUIContent.none);
                        EditorGUI.indentLevel--;
                    }

                    float nothingChance = 1f - probRetrigger.floatValue - probReduction.floatValue;
                    if (nothingChance < 0f) nothingChance = 0f;
                    EditorGUILayout.HelpBox(
                        $"Hiçbir şey olmama: %{nothingChance * 100f:F0}",
                        MessageType.Info);

                    //ödül düşme miktarı
                    SerializedProperty probAmount = choice.FindPropertyRelative("probRewardReductionAmount");
                    probAmount.isExpanded = EditorGUILayout.Foldout(probAmount.isExpanded, "Düşme Miktarı", true);
                    if (probAmount.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.Slider(probAmount, 0f, 1f, GUIContent.none);
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }

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
            EditorGUILayout.IntSlider(
                choice.FindPropertyRelative("eventBlockCycles"),
                0, 10, new GUIContent("Event Dondur (Dönem)"));
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("blocksCeasefire"),
                new GUIContent("Ateşkes Engelle"));
            SerializedProperty blocksGroup = choice.FindPropertyRelative("blocksEventGroup");
            EditorGUILayout.PropertyField(blocksGroup, new GUIContent("Grubu Engelle"));
            if (blocksGroup.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("blockedGroup"),
                    new GUIContent("Engellenecek Grup"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty hasProbWarEnd = choice.FindPropertyRelative("hasProbabilisticWarEnd");
            EditorGUILayout.PropertyField(hasProbWarEnd, new GUIContent("Olasılıklı Savaşı Bitir"));
            if (hasProbWarEnd.boolValue)
            {
                EditorGUI.indentLevel++;

                SerializedProperty probWarEnd = choice.FindPropertyRelative("probWarEndChance");
                SerializedProperty probDismiss = choice.FindPropertyRelative("probDismissChance");

                //savaşın bitme şansı — başlık tıklanınca slider açılır
                probWarEnd.isExpanded = EditorGUILayout.Foldout(probWarEnd.isExpanded, "Savaşın Bitme Şansı", true);
                if (probWarEnd.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Slider(probWarEnd, 0f, 1f, GUIContent.none);
                    EditorGUI.indentLevel--;
                }

                //ikna olma şansı — başlık tıklanınca slider açılır
                probDismiss.isExpanded = EditorGUILayout.Foldout(probDismiss.isExpanded, "İkna Olma Şansı", true);
                if (probDismiss.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Slider(probDismiss, 0f, 1f, GUIContent.none);
                    EditorGUI.indentLevel--;
                }

                float retrigger = 1f - probWarEnd.floatValue - probDismiss.floatValue;
                if (retrigger < 0f) retrigger = 0f;
                EditorGUILayout.HelpBox(
                    $"Tekrar: %{retrigger * 100f:F0} | Base değerler (support=50)",
                    MessageType.Info);

                //gecikme — başlık tıklanınca alan açılır
                SerializedProperty probDelay = choice.FindPropertyRelative("probWarEndDelay");
                probDelay.isExpanded = EditorGUILayout.Foldout(probDelay.isExpanded, "Gecikme (sn)", true);
                if (probDelay.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(probDelay, GUIContent.none);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //feed sonuçları — foldout
        if (!feedFoldouts.ContainsKey(index))
            feedFoldouts[index] = false;
        feedFoldouts[index] = EditorGUILayout.Foldout(
            feedFoldouts[index], "Feed Sonuçları", true);

        if (feedFoldouts[index])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("freezesFeed"),
                new GUIContent("Feed Dondur"));
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("slowsFeed"),
                new GUIContent("Feed Yavaşlat"));

            SerializedProperty hasFeedOverride = choice.FindPropertyRelative("hasFeedOverride");
            EditorGUILayout.PropertyField(hasFeedOverride, new GUIContent("Feed Yönlendir"));
            if (hasFeedOverride.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("feedOverrideTopic"),
                    new GUIContent("Konu"));
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("feedOverrideRatio"),
                    new GUIContent("Oran"));

                SerializedProperty hasCounter = choice.FindPropertyRelative("hasCounterFeedTopic");
                EditorGUILayout.PropertyField(hasCounter, new GUIContent("Counter Topic"));
                if (hasCounter.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("counterFeedTopic"),
                        new GUIContent("Konu"));
                    SerializedProperty counterRatio = choice.FindPropertyRelative("counterFeedRatio");
                    float maxCounter = 1f - choice.FindPropertyRelative("feedOverrideRatio").floatValue;
                    EditorGUILayout.Slider(counterRatio, 0f, maxCounter, new GUIContent("Oran"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("feedOverrideDuration"),
                    new GUIContent("Süre (sn)"));
                EditorGUI.indentLevel--;
            }

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

        //vandalizm etkileri — foldout
        if (!vandalismFoldouts.ContainsKey(index))
            vandalismFoldouts[index] = false;
        vandalismFoldouts[index] = EditorGUILayout.Foldout(
            vandalismFoldouts[index], "Vandalizm Etkisi", true);

        if (vandalismFoldouts[index])
        {
            EditorGUI.indentLevel++;

            SerializedProperty affectsVandalism = choice.FindPropertyRelative("affectsVandalism");
            EditorGUILayout.PropertyField(affectsVandalism, new GUIContent("Vandalizmi Etkiler"));

            if (affectsVandalism.boolValue)
            {
                SerializedProperty changeType = choice.FindPropertyRelative("vandalismChangeType");
                EditorGUILayout.PropertyField(changeType, new GUIContent("Değişim Tipi"));

                EditorGUI.indentLevel++;
                if ((VandalismChangeType)changeType.enumValueIndex == VandalismChangeType.Direct)
                {
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("vandalismTargetLevel"),
                        new GUIContent("Hedef Seviye"));
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("vandalismLevelDelta"),
                        new GUIContent("Seviye Değişimi (+/-)"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //medya takibi etkileri — foldout
        if (!mediaPursuitFoldouts.ContainsKey(index))
            mediaPursuitFoldouts[index] = false;
        mediaPursuitFoldouts[index] = EditorGUILayout.Foldout(
            mediaPursuitFoldouts[index], "Medya Takibi Etkisi", true);

        if (mediaPursuitFoldouts[index])
        {
            EditorGUI.indentLevel++;

            SerializedProperty affectsMediaPursuit = choice.FindPropertyRelative("affectsMediaPursuit");
            EditorGUILayout.PropertyField(affectsMediaPursuit, new GUIContent("Medya Takibini Etkiler"));

            if (affectsMediaPursuit.boolValue)
            {
                SerializedProperty mpChangeType = choice.FindPropertyRelative("mediaPursuitChangeType");
                EditorGUILayout.PropertyField(mpChangeType, new GUIContent("Değişim Tipi"));

                EditorGUI.indentLevel++;
                if ((MediaPursuitChangeType)mpChangeType.enumValueIndex == MediaPursuitChangeType.Direct)
                {
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("mediaPursuitTargetLevel"),
                        new GUIContent("Hedef Seviye"));
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("mediaPursuitLevelDelta"),
                        new GUIContent("Seviye Değişimi (+/-)"));
                }
                EditorGUI.indentLevel--;
            }

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
        choice.FindPropertyRelative("wealthModifier").floatValue = 0f;
        choice.FindPropertyRelative("cornerGrabModifier").floatValue = 0f;
        choice.FindPropertyRelative("protestModifier").floatValue = 0f;
        choice.FindPropertyRelative("protestTriggerChanceBonus").floatValue = 0f;
        choice.FindPropertyRelative("hasProtestChance").boolValue = false;
        choice.FindPropertyRelative("protestDecreaseChance").floatValue = 0f;
        choice.FindPropertyRelative("protestDecreaseAmount").floatValue = 0f;
        choice.FindPropertyRelative("protestIncreaseAmount").floatValue = 0f;
        choice.FindPropertyRelative("endsWar").boolValue = false;
        choice.FindPropertyRelative("warEndDelay").floatValue = 0f;
        choice.FindPropertyRelative("reducesReward").boolValue = false;
        choice.FindPropertyRelative("baseRewardReduction").floatValue = 0f;
        choice.FindPropertyRelative("endsWarWithDeal").boolValue = false;
        choice.FindPropertyRelative("dealDelay").floatValue = 0f;
        choice.FindPropertyRelative("dealRewardRatio").floatValue = 0f;
        choice.FindPropertyRelative("blocksEvents").boolValue = false;
        choice.FindPropertyRelative("eventBlockCycles").intValue = 0;
        choice.FindPropertyRelative("blocksCeasefire").boolValue = false;
        choice.FindPropertyRelative("blocksEventGroup").boolValue = false;
        choice.FindPropertyRelative("blockedGroup").objectReferenceValue = null;
        choice.FindPropertyRelative("hasProbabilisticRewardReduction").boolValue = false;
        choice.FindPropertyRelative("probRetriggerChance").floatValue = 0f;
        choice.FindPropertyRelative("probRewardReductionChance").floatValue = 0f;
        choice.FindPropertyRelative("probRewardReductionAmount").floatValue = 0f;
        choice.FindPropertyRelative("hasProbabilisticWarEnd").boolValue = false;
        choice.FindPropertyRelative("probWarEndChance").floatValue = 0f;
        choice.FindPropertyRelative("probDismissChance").floatValue = 0f;
        choice.FindPropertyRelative("probWarEndDelay").floatValue = 0f;
        choice.FindPropertyRelative("freezesFeed").boolValue = false;
        choice.FindPropertyRelative("slowsFeed").boolValue = false;
        choice.FindPropertyRelative("hasFeedOverride").boolValue = false;
        choice.FindPropertyRelative("feedOverrideTopic").enumValueIndex = 0;
        choice.FindPropertyRelative("feedOverrideRatio").floatValue = 0f;
        choice.FindPropertyRelative("hasCounterFeedTopic").boolValue = false;
        choice.FindPropertyRelative("counterFeedTopic").enumValueIndex = 0;
        choice.FindPropertyRelative("counterFeedRatio").floatValue = 0f;
        choice.FindPropertyRelative("feedOverrideDuration").floatValue = 0f;
        choice.FindPropertyRelative("continuesChain").boolValue = false;
        choice.FindPropertyRelative("isChainRefusal").boolValue = false;
        choice.FindPropertyRelative("triggersCeasefire").boolValue = false;
        choice.FindPropertyRelative("acceptsRivalDeal").boolValue = false;
        choice.FindPropertyRelative("rejectsRivalDeal").boolValue = false;
        choice.FindPropertyRelative("affectsVandalism").boolValue = false;
        choice.FindPropertyRelative("vandalismChangeType").enumValueIndex = 0;
        choice.FindPropertyRelative("vandalismTargetLevel").enumValueIndex = 0;
        choice.FindPropertyRelative("vandalismLevelDelta").intValue = 0;
        choice.FindPropertyRelative("affectsMediaPursuit").boolValue = false;
        choice.FindPropertyRelative("mediaPursuitChangeType").enumValueIndex = 0;
        choice.FindPropertyRelative("mediaPursuitTargetLevel").enumValueIndex = 0;
        choice.FindPropertyRelative("mediaPursuitLevelDelta").intValue = 0;
        choice.FindPropertyRelative("requiredSkills").ClearArray();
        choice.FindPropertyRelative("statConditions").ClearArray();
    }
}
