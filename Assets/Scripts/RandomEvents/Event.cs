using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Event")]
public class Event : ScriptableObject
{
    public string id;//eventin ID'si
    public string displayName; //ismi
    public string description; //açıklaması
    public Sprite icon; //iconu
    public List<EventChoice> choices; //bir evente verilebilecek seçimler.

    public bool isRepeatable = true; //tekrar gelip gelemeyeceği.
    public float weight = 1f; //ağırlığı, ne sıklıkla gelebileceğini etkiler.
    public List<Skill> requiredSkills; //event havuzuna girebilmek için hangi skillerin açılması gerektiği
    public List<StatCondition> statConditions; //bazı eventler stat durumuna göre de gelebilir. para seviyesine, güvenilirlik ve şüphesine göre vs.
    public int minGamePhase = 0; //oyunun min hangi noktasında gelebileceği.
    public int maxGamePhase = 99; //oyunun max hangi noktasında gelebileceği.
}
