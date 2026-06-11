using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EventChoice //event geldiğinde oyuncuya sunulacak seçenek.
{
    public string text;
    [SerializeReference] public List<SkillEffect> effects = new List<SkillEffect>();

    [Header("Feed Override (opsiyonel)")]
    public bool overridesFeed = false; //bu seçim feed'i etkiler mi
    public TopicType feedTopic; //hangi topic öne çıkacak
    public float feedOverrideRatio = 0.5f; //feed'deki oran (0-1 arası)
    public float feedOverrideDuration = 60f; //ne kadar süre etkili olacak (saniye)

    [Header("Feed Speed Boost (opsiyonel)")]
    public bool boostsFeedSpeed = false; //bu seçim feed hızını etkiler mi
    public float boostedMinInterval = 1f; //hızlandırılmış min süre (min 1 sn)
    public float boostedMaxInterval = 2f; //hızlandırılmış max süre
    public float speedBoostDuration = 60f; //hız boost süresi
}

/*
ÖRNEK KULLANIM:

Event: "Bir memur sana rüşvet teklif ediyor"
├── Choice 1:
│   ├── text: "Kabul et"
│   └── effects: [Wealth +5000, Suspicion +15]
│
├── Choice 2:
│   ├── text: "Reddet"
│   └── effects: [Trust +10]
│
└── Choice 3:
    ├── text: "Polise ihbar et"
    └── effects: [Suspicion -10, Wealth -2000]
    
*/
