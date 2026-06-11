using System.Collections.Generic;
using UnityEngine;

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
    [Header("Diğer Ön Koşullar")]
    public OtherPrerequisite otherPrerequisites; //tiklenebilir ek gereksinimler
    public List<Skill> blocksSkills; //bu skill açılınca hangi skillerin sonsuza kadar kilitlenmesi gerektiği
}
