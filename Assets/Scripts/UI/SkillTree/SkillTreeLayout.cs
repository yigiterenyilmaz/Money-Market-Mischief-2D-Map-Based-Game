using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill'in ait olduğu ağaç. Flags — bir skill birden fazla ağaca ait olabilir
/// (finance/politics özel skilleri gibi).
/// </summary>
[Flags]
public enum SkillBranch
{
    None = 0,
    Finance = 1 << 0,
    Politics = 1 << 1,
    Media = 1 << 2,
}

/// <summary>
/// Tek bir skill'in ağaçtaki yeri. column = ön koşul derinliği (soldan sağa),
/// row = dikey sıra (ağaçlar üst üste dizilir, aralarında hybrid'ler için boşluk kalır).
/// </summary>
[Serializable]
public class SkillTreeNodeLayout
{
    public string skillId;
    public SkillBranch branches = SkillBranch.None;
    public int column;
    public float row;
    [Tooltip("drawio'daki renk etiketi — tasarım notu olarak taşınır")]
    public string colorTag;
}

/// <summary>
/// Ağacın yerleşim tablosu. drawio dosyasından üretilir; skill verisinden (Skill.asset)
/// ayrı tutulur ki tasarım değiştiğinde tek asset yeniden üretilsin.
/// </summary>
[CreateAssetMenu(menuName = "SkillTree/Layout")]
public class SkillTreeLayout : ScriptableObject
{
    public List<SkillTreeNodeLayout> nodes = new List<SkillTreeNodeLayout>();

    [Header("Ağaç Başlıkları")]
    public List<BranchHeader> headers = new List<BranchHeader>();

    private Dictionary<string, SkillTreeNodeLayout> lookup;

    [Serializable]
    public class BranchHeader
    {
        public SkillBranch branch;
        public string title;
        public float row;      //başlık yazısının dikey konumu
        public float rowStart; //ağaç bandının üst sırası
        public float rowEnd;   //ağaç bandının alt sırası
    }

    public SkillTreeNodeLayout Get(string skillId)
    {
        if (lookup == null || lookup.Count != nodes.Count)
        {
            lookup = new Dictionary<string, SkillTreeNodeLayout>(nodes.Count);
            foreach (SkillTreeNodeLayout n in nodes)
            {
                if (n != null && !string.IsNullOrEmpty(n.skillId))
                    lookup[n.skillId] = n;
            }
        }

        return lookup.TryGetValue(skillId, out SkillTreeNodeLayout result) ? result : null;
    }
}
