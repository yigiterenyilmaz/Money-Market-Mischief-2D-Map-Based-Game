using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill'in aktif (oyuncunun tetiklediği) yeteneği.
/// Açılınca pasif çalışan effects'ten farklı olarak, bu efektler her kullanımda uygulanır
/// ve ardından bekleme süresi başlar.
/// </summary>
[Serializable]
public class SkillActiveAbility
{
    public bool enabled; //bu skill tıklanınca kullanılabilir mi
    public string abilityName;
    [TextArea(2, 5)] public string description;
    [Tooltip("Kullanımdan sonra tekrar hazır olana kadar geçen süre (saniye)")]
    public float cooldownSeconds = 30f;
    [SerializeReference] public List<SkillEffect> onActivate = new List<SkillEffect>();
}

[CreateAssetMenu(menuName = "SkillTree/Skill")]
public class Skill : ScriptableObject
{
    public string id; //skill id si
    public string displayName; //skill
    [TextArea(2, 8)] public string description; //skillin ne yaptığını anlatan açıklama
    public Sprite icon; //skill in iconu
    public int cost; //skill in bedeli
    public List<Skill> prerequisites; //skill in ön koşulları
    [SerializeReference] public List<SkillEffect> effects = new List<SkillEffect>(); //skill açılınca oluşan efektler
    public SkillActiveAbility activeAbility = new SkillActiveAbility(); //varsa oyuncunun tetiklediği yetenek
    [Header("Diğer Ön Koşullar")]
    public OtherPrerequisite otherPrerequisites; //tiklenebilir ek gereksinimler
    public List<Skill> blocksSkills; //bu skill açılınca hangi skillerin sonsuza kadar kilitlenmesi gerektiği
}
