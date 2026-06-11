using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PassiveIncomeEffect : SkillEffect
{
    public List<PassiveIncomeProduct> products; //bu skill açılınca satın alınabilir hale gelen ürünler

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null && products != null)
        {
            SkillTreeManager.Instance.UnlockProducts(products);
        }
    }
}
