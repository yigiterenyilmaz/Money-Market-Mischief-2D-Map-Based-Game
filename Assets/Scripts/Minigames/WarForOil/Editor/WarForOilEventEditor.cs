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
    private Dictionary<int, bool> permanentEffectFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> statCeilingFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> womanProcessFoldouts = new Dictionary<int, bool>();
    private bool chainFoldout;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        //choices, repeat alanları, defaultChoiceIndex, narrative ve zincir alanları hariç tüm alanları çiz
        //yazı makinesi efekti açıksa displayName gizlenir (typewriter modunda başlık yok)
        List<string> excludeList = new List<string> {
            "choices", "isUnlimitedRepeat", "maxRepeatCount", "defaultChoiceIndex",
            "hasNarrative", "narrative",
            "isVandalismEvent", "vandalismLevelOnTrigger", "startsVandalism", "forcesVandalismStart",
            "isMediaPursuitEvent", "mediaPursuitLevelOnTrigger",
            "isWomanProcessEvent", "minObsession", "maxObsession", "blockedWomanProcessEvents",
            "useTypewriterEffect",
            "hasPrecursorEvent", "precursorEventType", "precursorWarEvent", "precursorRandomEvent",
            "chainRole", "blocksSubChainBranching", "alsoBlockedBranchEvents",
            "minWarTime", "maxWarTime",
            "conditionalDescriptions",
            "useTypewriterEffect",
            "requiresBothProcessesActive"
        };
        if (serializedObject.FindProperty("useTypewriterEffect").boolValue)
            excludeList.Add("displayName");
        DrawPropertiesExcluding(serializedObject, excludeList.ToArray());

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

        //min/maxWarTime — kadın süreci eventlerinde gizle (savaş zamanlamasıyla ilgisi yok)
        if (!serializedObject.FindProperty("isWomanProcessEvent").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minWarTime"),
                new GUIContent("Min War Time"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWarTime"),
                new GUIContent("Max War Time"));
            EditorGUILayout.Space();
        }

        //narrative — tiklenince büyük metin penceresi açılır
        SerializedProperty hasNarrative = serializedObject.FindProperty("hasNarrative");
        EditorGUILayout.PropertyField(hasNarrative, new GUIContent("Narrative Var mı?"));
        if (hasNarrative.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("narrative"),
                new GUIContent("Narrative"));
            EditorGUI.indentLevel--;
        }

        //yazı makinesi efekti — açıklama harf harf akar
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("useTypewriterEffect"),
            new GUIContent("Yazı Makinesi Efekti", "Açıklama harf harf akar. Kapalıysa direkt paragraf gösterilir."));

        //koşullu açıklamalar — hikaye bayrağına göre farklı açıklama
        SerializedProperty condDescs = serializedObject.FindProperty("conditionalDescriptions");
        EditorGUILayout.PropertyField(condDescs, new GUIContent("Koşullu Açıklamalar"), true);
        if (condDescs.arraySize > 0)
        {
            EditorGUILayout.HelpBox(
                "Hikaye bayrağı aktifse event açıklaması alternatif metinle değiştirilir. İlk eşleşen bayrak geçerli olur.",
                MessageType.Info);
        }

        EditorGUILayout.Space();

        //zincir ayarları — sadece None/Head seçimi, dallanma choice seviyesinde
        SerializedProperty chainRole = serializedObject.FindProperty("chainRole");
        EditorGUILayout.PropertyField(chainRole, new GUIContent("Zincir Rolü"));

        SerializedProperty blocksSubChain = serializedObject.FindProperty("blocksSubChainBranching");
        EditorGUILayout.PropertyField(blocksSubChain, new GUIContent("Alt Zincir Dallanmasını Kapat"));
        if (blocksSubChain.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("alsoBlockedBranchEvents"),
                new GUIContent("Birlikte Engelle"), true);
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox(
                "Bu event tetiklendikten sonra, kendisi ve listedeki event'ler başka zincirlerde dallanma hedefi olarak seçilemez.",
                MessageType.Info);
        }

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
            SerializedProperty startsVandalismProp = serializedObject.FindProperty("startsVandalism");
            EditorGUILayout.PropertyField(startsVandalismProp,
                new GUIContent("Vandalizm Başlatıyor mu?"));
            if (startsVandalismProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("forcesVandalismStart"),
                    new GUIContent("Sistemi Zorla"));
                EditorGUI.indentLevel--;
            }
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

        //ikili süreç koşulu
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("requiresBothProcessesActive"),
            new GUIContent("İkili Süreç Gerekli", "Sadece hem savaş hem kadın süreci aktifken tetiklenebilir."));

        //kadın süreci eventi
        SerializedProperty isWomanProcessEvent = serializedObject.FindProperty("isWomanProcessEvent");
        EditorGUILayout.PropertyField(isWomanProcessEvent, new GUIContent("Kadın Süreci Eventi"));
        if (isWomanProcessEvent.boolValue)
        {
            EditorGUI.indentLevel++;

            //obsesyon aralığı
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Obsesyon Aralığı");
            SerializedProperty minObs = serializedObject.FindProperty("minObsession");
            SerializedProperty maxObs = serializedObject.FindProperty("maxObsession");
            float minVal = minObs.floatValue;
            float maxVal = maxObs.floatValue;
            minVal = EditorGUILayout.FloatField(minVal, GUILayout.Width(40));
            EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, 0f, 100f);
            maxVal = EditorGUILayout.FloatField(maxVal, GUILayout.Width(40));
            minObs.floatValue = Mathf.Round(minVal);
            maxObs.floatValue = Mathf.Round(maxVal);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("blockedWomanProcessEvents"),
                new GUIContent("Yasaklanan Eventler"), true);

            //öncü event
            EditorGUILayout.Space(4);
            SerializedProperty hasPrecursor = serializedObject.FindProperty("hasPrecursorEvent");
            EditorGUILayout.PropertyField(hasPrecursor, new GUIContent("Öncü Event Var"));
            if (hasPrecursor.boolValue)
            {
                EditorGUI.indentLevel++;
                SerializedProperty precursorType = serializedObject.FindProperty("precursorEventType");
                EditorGUILayout.PropertyField(precursorType, new GUIContent("Öncü Tip"));

                if ((PrecursorEventType)precursorType.enumValueIndex == PrecursorEventType.WarForOil)
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("precursorWarEvent"),
                        new GUIContent("War For Oil Event"));
                    EditorGUILayout.HelpBox(
                        "Savaş yoksa bu kadın eventi ve öncü event ikisi de tetiklenmez.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("precursorRandomEvent"),
                        new GUIContent("Random Event"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "Obsesyon aralığı: Event sadece obsesyon bu aralıktayken havuzdan seçilebilir (0-100 = sınırsız).\nYasaklanan eventler: Bu event tetiklenince listedeki eventler havuzdan ve zincirlerden çıkarılır.\nÖncü event: Kadın eventi gelmeden önce bağlı event tetiklenir, 4sn sonra kadın eventi gelir.",
                MessageType.Info);
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
        SerializedProperty dispName = choice.FindPropertyRelative("displayName");
        EditorGUILayout.LabelField("Display Name");
        dispName.stringValue = EditorGUILayout.TextArea(dispName.stringValue, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
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

            //itibar tabanı — itibar'ın hemen altında
            SerializedProperty hasRepFloor = choice.FindPropertyRelative("hasReputationFloor");
            EditorGUILayout.PropertyField(hasRepFloor, new GUIContent("İtibar Tabanı"));
            if (hasRepFloor.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("reputationFloor"),
                    new GUIContent("Minimum İtibar"));
                EditorGUILayout.HelpBox(
                    "Bu choice yüzünden itibar bu değerin altına düşmez.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(choice.FindPropertyRelative("politicalInfluenceModifier"),
                new GUIContent("Politik Nüfuz"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("costModifier"),
                new GUIContent("Maliyet (Birikimli)"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("wealthModifier"),
                new GUIContent("Anlık Para"));
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("cornerGrabModifier"),
                new GUIContent("Köşe Kapma"));

            //kadın süreci modifier — sadece isWomanProcessEvent açıkken göster
            if (serializedObject.FindProperty("isWomanProcessEvent").boolValue)
            {
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("womanObsessionModifier"),
                    new GUIContent("Kadın Obsesyonu"));

                SerializedProperty hasObsFloor = choice.FindPropertyRelative("hasObsessionFloor");
                EditorGUILayout.PropertyField(hasObsFloor, new GUIContent("Obsesyon Tabanı"));
                if (hasObsFloor.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("obsessionFloor"),
                        new GUIContent("Minimum Obsesyon"));
                    EditorGUILayout.HelpBox(
                        "Bu choice yüzünden obsesyon bu değerin altına düşmez.",
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }

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

            //kadın süreci — başlat/bitir yan yana, aynı anda ikisi seçilemez
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Kadın Süreci");
            SerializedProperty startsWP = choice.FindPropertyRelative("startsWomanProcess");
            SerializedProperty endsWP = choice.FindPropertyRelative("endsWomanProcess");
            SerializedProperty freezesWP = choice.FindPropertyRelative("freezesWomanProcess");
            bool newStarts = GUILayout.Toggle(startsWP.boolValue, "Başlat", EditorStyles.miniButtonLeft, GUILayout.Width(60));
            bool newEnds = GUILayout.Toggle(endsWP.boolValue, "Bitir", EditorStyles.miniButtonMid, GUILayout.Width(60));
            bool newFreezes = GUILayout.Toggle(freezesWP.boolValue, "Dondur", EditorStyles.miniButtonRight, GUILayout.Width(60));
            //biri aktifleşince diğerlerini kapat
            if (newStarts && !startsWP.boolValue) { newEnds = false; newFreezes = false; }
            if (newEnds && !endsWP.boolValue) { newStarts = false; newFreezes = false; }
            if (newFreezes && !freezesWP.boolValue) { newStarts = false; newEnds = false; }
            startsWP.boolValue = newStarts;
            endsWP.boolValue = newEnds;
            freezesWP.boolValue = newFreezes;
            EditorGUILayout.EndHorizontal();

            if (freezesWP.boolValue)
            {
                EditorGUI.indentLevel++;
                SerializedProperty freezeCycles = choice.FindPropertyRelative("womanProcessFreezeCycles");
                freezeCycles.intValue = EditorGUILayout.IntField(
                    new GUIContent("Dondurma (döngü)"), freezeCycles.intValue);
                if (freezeCycles.intValue < 1) freezeCycles.intValue = 1;
                EditorGUILayout.HelpBox(
                    $"Kadın süreci {freezeCycles.intValue} döngü boyunca tetiklenmez. Mevcut dondurma varsa üstüne eklenir.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            //kadın süreci havuz yönlendirme — sadece kadın süreci eventlerinde göster
            if (serializedObject.FindProperty("isWomanProcessEvent").boolValue)
            {
                SerializedProperty redirectsPool = choice.FindPropertyRelative("redirectsWomanPool");
                EditorGUILayout.PropertyField(redirectsPool, new GUIContent("Havuzu Yönlendir"));
                if (redirectsPool.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("womanPoolDatabase"),
                        new GUIContent("Hedef Database"));
                    EditorGUILayout.HelpBox(
                        "Bu choice seçildiğinde kadın süreci kalıcı olarak bu database'deki havuzlardan event çekmeye başlar.",
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }

                SerializedProperty hasDropLimit = choice.FindPropertyRelative("hasObsessionDropLimit");
                EditorGUILayout.PropertyField(hasDropLimit, new GUIContent("Obsesyon Düşüş Limiti"));
                if (hasDropLimit.boolValue)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty dropLimit = choice.FindPropertyRelative("obsessionDropLimit");
                    dropLimit.floatValue = EditorGUILayout.FloatField(
                        new GUIContent("Düşüş Miktarı"), dropLimit.floatValue);
                    if (dropLimit.floatValue < 0f) dropLimit.floatValue = 0f;
                    EditorGUILayout.HelpBox(
                        $"Bu choice seçildiğinde obsesyon o anki değerinden {dropLimit.floatValue} düşerse kadın süreci otomatik sona erer.",
                        MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }

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

            SerializedProperty winsWar = choice.FindPropertyRelative("winsWar");
            EditorGUILayout.PropertyField(winsWar, new GUIContent("Savaşı Kazan",
                "Savaşı direkt kazandırır. Zar atılmaz, garanti zafer."));
            if (winsWar.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    choice.FindPropertyRelative("winWarDelay"),
                    new GUIContent("Gecikme (sn)"));

                SerializedProperty customReward = choice.FindPropertyRelative("winWarCustomReward");
                string[] rewardOptions = { "War Support Tabanlı", "Direkt Oran Gir" };
                int rewardSel = customReward.boolValue ? 1 : 0;
                rewardSel = EditorGUILayout.Popup("Ödül Hesaplama", rewardSel, rewardOptions);
                customReward.boolValue = rewardSel == 1;

                if (customReward.boolValue)
                {
                    EditorGUILayout.Slider(
                        choice.FindPropertyRelative("winWarRewardRatio"),
                        0f, 1f, new GUIContent("Ödül Oranı",
                        "1 = tam ödül, 0.5 = yarı ödül"));
                }

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
                new GUIContent("WFO Engelle",
                "Tiklenirse bu savaş bitene kadar hiçbir savaş eventi gelmez."));
            EditorGUILayout.IntSlider(
                choice.FindPropertyRelative("eventBlockCycles"),
                0, 10, new GUIContent("Savaş Event Dondur (Dönem)",
                "Bu kadar event dönemi boyunca savaş eventleri gelmez."));
            EditorGUILayout.IntSlider(
                choice.FindPropertyRelative("globalEventBlockCycles"),
                0, 10, new GUIContent("Global Event Dondur (Dönem)",
                "Kadın eventleri HARİÇ tüm eventleri (savaş + random) bu kadar dönem boyunca durdurur."));
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

            //anında event tetikleme — choice seçildiğinde havuzdan biri direkt gösterilir
            SerializedProperty hasImmediate = choice.FindPropertyRelative("hasImmediateEvent");
            EditorGUILayout.PropertyField(hasImmediate, new GUIContent("Anında Event Tetikle"));
            if (hasImmediate.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Slider(
                    choice.FindPropertyRelative("immediateEventDelay"),
                    0f, 15f, new GUIContent("Gecikme (sn)"));

                SerializedProperty isTiered = choice.FindPropertyRelative("immediateEventIsTiered");
                EditorGUILayout.PropertyField(isTiered, new GUIContent("Kadın Obsesyon Tier'ına Göre"));

                if (isTiered.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("immediateEventTier1"),
                        new GUIContent("Low Obsesyon Event"));
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("immediateEventTier2"),
                        new GUIContent("Mid Obsesyon Event"));
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("immediateEventTier3"),
                        new GUIContent("High Obsesyon Event"));
                }
                else
                {
                    DrawImmediateEventPool(choice.FindPropertyRelative("immediateEventPool"));
                }
                EditorGUI.indentLevel--;
            }

            //hikaye bayrakları — bu choice seçildiğinde aktif edilen bayraklar
            EditorGUILayout.PropertyField(
                choice.FindPropertyRelative("setsStoryFlags"),
                new GUIContent("Hikaye Bayrakları"), true);

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

        //zincir dallanması — foldout (chainBranches listesi)
        if (!chainChoiceFoldouts.ContainsKey(index))
            chainChoiceFoldouts[index] = false;
        SerializedProperty chainBranches = choice.FindPropertyRelative("chainBranches");
        chainChoiceFoldouts[index] = EditorGUILayout.Foldout(
            chainChoiceFoldouts[index], $"Zincir Dallanması ({chainBranches.arraySize})", true);

        if (chainChoiceFoldouts[index])
        {
            EditorGUI.indentLevel++;

            //etkileyen stat seçimi (choice seviyesinde — tüm branch'ler paylaşır)
            SerializedProperty chainInfluenceStat = choice.FindPropertyRelative("chainInfluenceStat");
            EditorGUILayout.PropertyField(chainInfluenceStat, new GUIContent("Etkileyen Stat"));

            bool isJustLuck = (ChainInfluenceStat)chainInfluenceStat.enumValueIndex == ChainInfluenceStat.JustLuck;

            //eşik değerleri — sadece stat bazlıysa göster
            string range0Label, range1Label, range2Label, range3Label;
            if (!isJustLuck)
            {
                SerializedProperty t0 = choice.FindPropertyRelative("chainThreshold0");
                SerializedProperty t1 = choice.FindPropertyRelative("chainThreshold1");
                SerializedProperty t2 = choice.FindPropertyRelative("chainThreshold2");
                EditorGUILayout.PropertyField(t0, new GUIContent("Eşik 1 (%)"));
                EditorGUILayout.PropertyField(t1, new GUIContent("Eşik 2 (%)"));
                EditorGUILayout.PropertyField(t2, new GUIContent("Eşik 3 (%)"));

                //eşik doğrulama
                if (t0.floatValue >= t1.floatValue || t1.floatValue >= t2.floatValue)
                    EditorGUILayout.HelpBox("Eşikler sıralı olmalı: Eşik 1 < Eşik 2 < Eşik 3", MessageType.Warning);

                range0Label = $"0-{t0.floatValue:F0}%";
                range1Label = $"{t0.floatValue:F0}-{t1.floatValue:F0}%";
                range2Label = $"{t1.floatValue:F0}-{t2.floatValue:F0}%";
                range3Label = $"{t2.floatValue:F0}-100%";
            }
            else
            {
                range0Label = range1Label = range2Label = range3Label = "";
            }

            EditorGUILayout.Space(4);

            //branch listesi
            for (int b = 0; b < chainBranches.arraySize; b++)
            {
                SerializedProperty branch = chainBranches.GetArrayElementAtIndex(b);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    branch.FindPropertyRelative("targetEvent"),
                    new GUIContent($"Hedef {b}"));
                if (GUILayout.Button("\u2212", GUILayout.Width(20)))
                {
                    chainBranches.DeleteArrayElementAtIndex(b);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (b < chainBranches.arraySize) //silindiyse atla
                {
                    EditorGUI.indentLevel++;
                    if (isJustLuck)
                    {
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("weightRange0"),
                            0f, 1f, new GUIContent("Ağırlık"));
                    }
                    else
                    {
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("weightRange0"),
                            0f, 1f, new GUIContent(range0Label));
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("weightRange1"),
                            0f, 1f, new GUIContent(range1Label));
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("weightRange2"),
                            0f, 1f, new GUIContent(range2Label));
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("weightRange3"),
                            0f, 1f, new GUIContent(range3Label));
                    }
                    SerializedProperty triggersImmediate = branch.FindPropertyRelative("triggersAsImmediateEvent");
                    EditorGUILayout.PropertyField(triggersImmediate,
                        new GUIContent("Anında Event Olarak Tetikle"));
                    if (triggersImmediate.boolValue)
                    {
                        EditorGUILayout.Slider(
                            branch.FindPropertyRelative("immediateEventDelay"),
                            0f, 15f, new GUIContent("Gecikme (sn)"));
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                }
            }

            if (GUILayout.Button("+ Dal Ekle"))
            {
                chainBranches.InsertArrayElementAtIndex(chainBranches.arraySize);
                SerializedProperty newBranch = chainBranches.GetArrayElementAtIndex(chainBranches.arraySize - 1);
                newBranch.FindPropertyRelative("targetEvent").objectReferenceValue = null;
                newBranch.FindPropertyRelative("weightRange0").floatValue = 0f;
                newBranch.FindPropertyRelative("weightRange1").floatValue = 0f;
                newBranch.FindPropertyRelative("weightRange2").floatValue = 0f;
                newBranch.FindPropertyRelative("weightRange3").floatValue = 0f;
                newBranch.FindPropertyRelative("triggersAsImmediateEvent").boolValue = false;
                newBranch.FindPropertyRelative("immediateEventDelay").floatValue = 0f;
            }

            //chain bitme şansı — dallanma varsa göster
            SerializedProperty chainCanEnd = choice.FindPropertyRelative("chainCanEnd");
            float endWeight = 0f;
            if (chainBranches.arraySize > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(chainCanEnd, new GUIContent("Bitme Şansı"));
                if (chainCanEnd.boolValue)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty endWeightProp = choice.FindPropertyRelative("chainEndWeight");
                    EditorGUILayout.Slider(endWeightProp, 0f, 1f, new GUIContent("Bitme Ağırlığı"));
                    endWeight = endWeightProp.floatValue;
                    EditorGUI.indentLevel--;
                }
            }

            //koşullu dallanma tanımı — choice seviyesinde tek koşul
            if (chainBranches.arraySize > 0)
            {
                EditorGUILayout.Space(2);
                SerializedProperty hasCondBranch = choice.FindPropertyRelative("hasConditionalBranching");
                EditorGUILayout.PropertyField(hasCondBranch, new GUIContent("Koşullu Dallanma"));
                if (hasCondBranch.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("branchCounterKey"),
                        new GUIContent("Sayaç Adı"));
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("branchCounterMin"),
                        new GUIContent("Min Değer"));
                    SerializedProperty maxVal = choice.FindPropertyRelative("branchCounterMax");
                    EditorGUILayout.PropertyField(maxVal, new GUIContent("Max Değer (-1 = sınırsız)"));
                    EditorGUILayout.HelpBox(
                        "Sayaç bu aralıktaysa koşullu dallardan, değilse yukarıdaki normal dallardan seçilir.",
                        MessageType.Info);

                    //koşullu dallar listesi
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Koşullu Dallar", EditorStyles.boldLabel);
                    SerializedProperty condBranches = choice.FindPropertyRelative("conditionalChainBranches");
                    for (int cb = 0; cb < condBranches.arraySize; cb++)
                    {
                        SerializedProperty condBranch = condBranches.GetArrayElementAtIndex(cb);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(
                            condBranch.FindPropertyRelative("targetEvent"),
                            new GUIContent("Hedef " + cb));
                        if (GUILayout.Button("-", GUILayout.Width(20)))
                        {
                            //object reference varsa önce null'la, sonra sil
                            SerializedProperty targetRef = condBranches.GetArrayElementAtIndex(cb).FindPropertyRelative("targetEvent");
                            if (targetRef != null && targetRef.objectReferenceValue != null)
                                targetRef.objectReferenceValue = null;
                            condBranches.DeleteArrayElementAtIndex(cb);
                            cb--;
                            EditorGUILayout.EndHorizontal();
                            continue;
                        }
                        EditorGUILayout.EndHorizontal();

                        if (cb >= 0 && cb < condBranches.arraySize)
                        {
                            EditorGUI.indentLevel++;
                            if (isJustLuck)
                            {
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("weightRange0"),
                                    0f, 1f, new GUIContent("Ağırlık"));
                            }
                            else
                            {
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("weightRange0"),
                                    0f, 1f, new GUIContent(range0Label));
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("weightRange1"),
                                    0f, 1f, new GUIContent(range1Label));
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("weightRange2"),
                                    0f, 1f, new GUIContent(range2Label));
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("weightRange3"),
                                    0f, 1f, new GUIContent(range3Label));
                            }
                            SerializedProperty condTriggersImmediate = condBranch.FindPropertyRelative("triggersAsImmediateEvent");
                            EditorGUILayout.PropertyField(condTriggersImmediate,
                                new GUIContent("Anında Event Olarak Tetikle"));
                            if (condTriggersImmediate.boolValue)
                            {
                                EditorGUILayout.Slider(
                                    condBranch.FindPropertyRelative("immediateEventDelay"),
                                    0f, 15f, new GUIContent("Gecikme (sn)"));
                            }
                            EditorGUI.indentLevel--;
                            EditorGUILayout.Space(2);
                        }
                    }

                    if (GUILayout.Button("+ Koşullu Dal Ekle"))
                    {
                        condBranches.InsertArrayElementAtIndex(condBranches.arraySize);
                        SerializedProperty newCond = condBranches.GetArrayElementAtIndex(condBranches.arraySize - 1);
                        newCond.FindPropertyRelative("targetEvent").objectReferenceValue = null;
                        newCond.FindPropertyRelative("weightRange0").floatValue = 0f;
                        newCond.FindPropertyRelative("weightRange1").floatValue = 0f;
                        newCond.FindPropertyRelative("weightRange2").floatValue = 0f;
                        newCond.FindPropertyRelative("weightRange3").floatValue = 0f;
                        newCond.FindPropertyRelative("triggersAsImmediateEvent").boolValue = false;
                        newCond.FindPropertyRelative("immediateEventDelay").floatValue = 0f;
                    }

                    EditorGUI.indentLevel--;
                }
            }

            //zincir sayaç sistemi
            {
                EditorGUILayout.Space(2);
                SerializedProperty incCounter = choice.FindPropertyRelative("incrementsChainCounter");
                EditorGUILayout.PropertyField(incCounter, new GUIContent("Zincir Sayacı Artır"));
                if (incCounter.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("chainCounterKey"),
                        new GUIContent("Sayaç Adı"));
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("chainCounterIncrement"),
                        new GUIContent("Artış Miktarı"));

                    SerializedProperty hasEarly = choice.FindPropertyRelative("hasEarlyChainTrigger");
                    EditorGUILayout.PropertyField(hasEarly, new GUIContent("Erken Tetikleme"));
                    if (hasEarly.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(
                            choice.FindPropertyRelative("earlyTriggerThreshold"),
                            new GUIContent("Eşik Değeri"));
                        EditorGUILayout.PropertyField(
                            choice.FindPropertyRelative("earlyTriggerEvent"),
                            new GUIContent("Hedef Event"));
                        EditorGUILayout.HelpBox(
                            "Sayaç bu eşiğe ulaşırsa zincir atlanıp direkt hedef event tetiklenir.",
                            MessageType.Info);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }
            }

            //zincir arası tick etkisi — dallanma varsa göster
            if (chainBranches.arraySize > 0)
            {
                EditorGUILayout.Space(2);
                SerializedProperty hasChainTick = choice.FindPropertyRelative("hasChainTickEffect");
                EditorGUILayout.PropertyField(hasChainTick, new GUIContent("Zincir Arası Tick Etkisi"));
                if (hasChainTick.boolValue)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty tickStatProp = choice.FindPropertyRelative("chainTickStat");
                    int tickStatIdx = tickStatProp.enumValueIndex;
                    string[] tickStatLabels = { "War Support", "Suspicion", "Reputation", "Political Influence" };
                    tickStatIdx = EditorGUILayout.Popup(new GUIContent("Hedef Stat"), tickStatIdx, tickStatLabels);
                    tickStatProp.enumValueIndex = tickStatIdx;
                    EditorGUILayout.PropertyField(
                        choice.FindPropertyRelative("chainTickAmount"),
                        new GUIContent("Tick Başına Miktar"));
                    EditorGUI.indentLevel--;
                }
            }

            //aralık başına toplam ağırlık doğrulaması (bitme ağırlığı dahil)
            if (chainBranches.arraySize > 0)
            {
                int rangeCount = isJustLuck ? 1 : 4;
                float[] sums = new float[rangeCount];
                for (int b = 0; b < chainBranches.arraySize; b++)
                {
                    SerializedProperty br = chainBranches.GetArrayElementAtIndex(b);
                    sums[0] += br.FindPropertyRelative("weightRange0").floatValue;
                    if (!isJustLuck)
                    {
                        sums[1] += br.FindPropertyRelative("weightRange1").floatValue;
                        sums[2] += br.FindPropertyRelative("weightRange2").floatValue;
                        sums[3] += br.FindPropertyRelative("weightRange3").floatValue;
                    }
                }

                //bitme ağırlığını tüm aralıklara ekle
                for (int r = 0; r < rangeCount; r++)
                    sums[r] += endWeight;

                string sumText;
                if (isJustLuck)
                {
                    sumText = $"Toplam: {sums[0]:F2}";
                }
                else
                {
                    sumText = $"{range0Label}: {sums[0]:F2} | {range1Label}: {sums[1]:F2} | {range2Label}: {sums[2]:F2} | {range3Label}: {sums[3]:F2}";
                }

                bool anyBad = false;
                for (int r = 0; r < rangeCount; r++)
                {
                    if (Mathf.Abs(sums[r] - 1f) > 0.01f) { anyBad = true; break; }
                }
                EditorGUILayout.HelpBox(sumText,
                    anyBad ? MessageType.Warning : MessageType.Info);
            }

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

        //kalıcı stat çarpanları — foldout (çoklu entry)
        if (!permanentEffectFoldouts.ContainsKey(index))
            permanentEffectFoldouts[index] = false;

        SerializedProperty permList = choice.FindPropertyRelative("permanentMultipliers");
        string permLabel = permList.arraySize > 0
            ? $"Kalıcı Stat Çarpanları ({permList.arraySize})"
            : "Kalıcı Stat Çarpanları";
        permanentEffectFoldouts[index] = EditorGUILayout.Foldout(
            permanentEffectFoldouts[index], permLabel, true);

        if (permanentEffectFoldouts[index])
        {
            EditorGUI.indentLevel++;

            for (int p = 0; p < permList.arraySize; p++)
            {
                SerializedProperty entry = permList.GetArrayElementAtIndex(p);
                float val = entry.FindPropertyRelative("multiplier").floatValue;
                string info = val > 1f
                    ? $"(%{(val - 1f) * 100f:F0} artış)"
                    : val < 1f
                        ? $"(%{(1f - val) * 100f:F0} azalış)"
                        : "(etkisiz)";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    entry.FindPropertyRelative("stat"),
                    GUIContent.none, GUILayout.MinWidth(80));
                EditorGUILayout.PropertyField(
                    entry.FindPropertyRelative("multiplier"),
                    GUIContent.none, GUILayout.Width(90));
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel, GUILayout.Width(90));
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    permList.DeleteArrayElementAtIndex(p);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Kalıcı Çarpan Ekle"))
            {
                permList.InsertArrayElementAtIndex(permList.arraySize);
                SerializedProperty newEntry = permList.GetArrayElementAtIndex(permList.arraySize - 1);
                newEntry.FindPropertyRelative("stat").enumValueIndex = 0;
                newEntry.FindPropertyRelative("multiplier").floatValue = 1f;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);

        //dinamik stat tavanı — foldout
        if (!statCeilingFoldouts.ContainsKey(index))
            statCeilingFoldouts[index] = false;

        SerializedProperty ceilingList = choice.FindPropertyRelative("statCeilingEffects");
        string ceilingLabel = ceilingList.arraySize > 0
            ? $"Stat Tavan Etkileri ({ceilingList.arraySize})"
            : "Stat Tavan Etkileri";
        statCeilingFoldouts[index] = EditorGUILayout.Foldout(
            statCeilingFoldouts[index], ceilingLabel, true);

        if (statCeilingFoldouts[index])
        {
            EditorGUI.indentLevel++;

            for (int c = 0; c < ceilingList.arraySize; c++)
            {
                SerializedProperty entry = ceilingList.GetArrayElementAtIndex(c);
                SerializedProperty modeProp = entry.FindPropertyRelative("mode");
                SerializedProperty stat = entry.FindPropertyRelative("stat");
                SerializedProperty val = entry.FindPropertyRelative("ceilingValue");
                SerializedProperty mult = entry.FindPropertyRelative("ceilingMultiplier");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Etki {c}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Kaldır", GUILayout.Width(60)))
                {
                    ceilingList.DeleteArrayElementAtIndex(c);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(stat, new GUIContent("Stat"));

                string[] options = { "Direkt Ata", "Çarpanla Düşür", "Tavanı Kaldır" };
                modeProp.enumValueIndex = EditorGUILayout.Popup("İşlem", modeProp.enumValueIndex, options);

                StatCeilingMode mode = (StatCeilingMode)modeProp.enumValueIndex;
                if (mode == StatCeilingMode.Set)
                {
                    EditorGUILayout.PropertyField(val, new GUIContent("Tavan Değeri"));
                }
                else if (mode == StatCeilingMode.Multiply)
                {
                    EditorGUILayout.Slider(mult, 0f, 1f, new GUIContent("Çarpan"));
                    EditorGUILayout.HelpBox("Mevcut tavan × çarpan = yeni tavan.\nÖrn: tavan 100, çarpan 0.5 → yeni tavan 50.", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button("+ Stat Tavanı Ekle"))
            {
                ceilingList.InsertArrayElementAtIndex(ceilingList.arraySize);
                SerializedProperty newEntry = ceilingList.GetArrayElementAtIndex(ceilingList.arraySize - 1);
                newEntry.FindPropertyRelative("stat").enumValueIndex = 0;
                newEntry.FindPropertyRelative("mode").enumValueIndex = 0;
                newEntry.FindPropertyRelative("ceilingValue").floatValue = 50f;
                newEntry.FindPropertyRelative("ceilingMultiplier").floatValue = 1f;
            }

            EditorGUILayout.HelpBox("Tavan Koy: Stat bu değerin üzerine çıkamaz.\nTavanı Kaldır: Önceden konmuş tavanı kaldırır, doğal sınıra döner.", MessageType.Info);

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

    private void DrawImmediateEventPool(SerializedProperty pool)
    {
        for (int ei = 0; ei < pool.arraySize; ei++)
        {
            SerializedProperty entry = pool.GetArrayElementAtIndex(ei);
            EditorGUILayout.PropertyField(
                entry.FindPropertyRelative("targetEvent"),
                new GUIContent($"Event {ei + 1}"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                entry.FindPropertyRelative("weight"),
                new GUIContent("  Ağırlık"));
            if (GUILayout.Button("-", EditorStyles.miniButtonRight, GUILayout.Width(22)))
            {
                pool.DeleteArrayElementAtIndex(ei);
                break;
            }
            EditorGUILayout.EndHorizontal();
            if (ei < pool.arraySize - 1)
                EditorGUILayout.Space(2);
        }
        //toplam yüzde hesapla
        float totalWeight = 0f;
        for (int ei2 = 0; ei2 < pool.arraySize; ei2++)
            totalWeight += pool.GetArrayElementAtIndex(ei2).FindPropertyRelative("weight").floatValue;
        if (pool.arraySize > 0)
        {
            if (Mathf.Abs(totalWeight - 100f) > 0.1f)
                EditorGUILayout.HelpBox($"Toplam: %{totalWeight:F0} (100 olmalı!)", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"Toplam: %{totalWeight:F0}", MessageType.Info);
        }
        if (GUILayout.Button("+ Event Ekle"))
            pool.InsertArrayElementAtIndex(pool.arraySize);
    }

    private void ClearChoice(SerializedProperty choice)
    {
        choice.FindPropertyRelative("displayName").stringValue = "";
        choice.FindPropertyRelative("description").stringValue = "";
        choice.FindPropertyRelative("supportModifier").floatValue = 0f;
        choice.FindPropertyRelative("suspicionModifier").floatValue = 0f;
        choice.FindPropertyRelative("reputationModifier").floatValue = 0f;
        choice.FindPropertyRelative("hasReputationFloor").boolValue = false;
        choice.FindPropertyRelative("reputationFloor").floatValue = 0f;
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
        choice.FindPropertyRelative("winsWar").boolValue = false;
        choice.FindPropertyRelative("winWarDelay").floatValue = 0f;
        choice.FindPropertyRelative("winWarCustomReward").boolValue = false;
        choice.FindPropertyRelative("winWarRewardRatio").floatValue = 1f;
        choice.FindPropertyRelative("reducesReward").boolValue = false;
        choice.FindPropertyRelative("baseRewardReduction").floatValue = 0f;
        choice.FindPropertyRelative("endsWarWithDeal").boolValue = false;
        choice.FindPropertyRelative("dealDelay").floatValue = 0f;
        choice.FindPropertyRelative("dealRewardRatio").floatValue = 0f;
        choice.FindPropertyRelative("blocksEvents").boolValue = false;
        choice.FindPropertyRelative("eventBlockCycles").intValue = 0;
        choice.FindPropertyRelative("globalEventBlockCycles").intValue = 0;
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
        choice.FindPropertyRelative("chainInfluenceStat").enumValueIndex = 0;
        choice.FindPropertyRelative("chainThreshold0").floatValue = 20f;
        choice.FindPropertyRelative("chainThreshold1").floatValue = 50f;
        choice.FindPropertyRelative("chainThreshold2").floatValue = 75f;
        choice.FindPropertyRelative("chainBranches").ClearArray();
        choice.FindPropertyRelative("conditionalChainBranches").ClearArray();
        choice.FindPropertyRelative("chainCanEnd").boolValue = false;
        choice.FindPropertyRelative("chainEndWeight").floatValue = 1f;
        choice.FindPropertyRelative("hasConditionalBranching").boolValue = false;
        choice.FindPropertyRelative("branchCounterKey").stringValue = "";
        choice.FindPropertyRelative("branchCounterMin").intValue = 0;
        choice.FindPropertyRelative("branchCounterMax").intValue = -1;
        choice.FindPropertyRelative("incrementsChainCounter").boolValue = false;
        choice.FindPropertyRelative("chainCounterKey").stringValue = "";
        choice.FindPropertyRelative("chainCounterIncrement").intValue = 1;
        choice.FindPropertyRelative("hasEarlyChainTrigger").boolValue = false;
        choice.FindPropertyRelative("earlyTriggerThreshold").intValue = 0;
        choice.FindPropertyRelative("earlyTriggerEvent").objectReferenceValue = null;
        choice.FindPropertyRelative("hasChainTickEffect").boolValue = false;
        choice.FindPropertyRelative("chainTickStat").enumValueIndex = 0;
        choice.FindPropertyRelative("chainTickAmount").floatValue = 0f;
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
        choice.FindPropertyRelative("startsWomanProcess").boolValue = false;
        choice.FindPropertyRelative("endsWomanProcess").boolValue = false;
        choice.FindPropertyRelative("womanObsessionModifier").floatValue = 0f;
        choice.FindPropertyRelative("hasObsessionFloor").boolValue = false;
        choice.FindPropertyRelative("obsessionFloor").floatValue = 0f;
        choice.FindPropertyRelative("redirectsWomanPool").boolValue = false;
        choice.FindPropertyRelative("freezesWomanProcess").boolValue = false;
        choice.FindPropertyRelative("womanProcessFreezeCycles").intValue = 1;
        choice.FindPropertyRelative("hasObsessionDropLimit").boolValue = false;
        choice.FindPropertyRelative("obsessionDropLimit").floatValue = 0f;
        choice.FindPropertyRelative("womanPoolDatabase").objectReferenceValue = null;
        choice.FindPropertyRelative("permanentMultipliers").ClearArray();
        choice.FindPropertyRelative("statCeilingEffects").ClearArray();
        choice.FindPropertyRelative("hasImmediateEvent").boolValue = false;
        choice.FindPropertyRelative("immediateEventDelay").floatValue = 0f;
        choice.FindPropertyRelative("immediateEventIsTiered").boolValue = false;
        choice.FindPropertyRelative("immediateEventPool").ClearArray();
        choice.FindPropertyRelative("immediateEventTier1").objectReferenceValue = null;
        choice.FindPropertyRelative("immediateEventTier2").objectReferenceValue = null;
        choice.FindPropertyRelative("immediateEventTier3").objectReferenceValue = null;
        choice.FindPropertyRelative("setsStoryFlags").ClearArray();
        choice.FindPropertyRelative("requiredSkills").ClearArray();
        choice.FindPropertyRelative("statConditions").ClearArray();
    }
}
