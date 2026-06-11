using System.Collections.Generic;

[System.Serializable]
public class InvestmentEffect : SkillEffect
{
    public List<InvestmentProduct> products; //bu skill açılınca satın alınabilir hale gelen yatırım ürünleri

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null && products != null)
        {
            SkillTreeManager.Instance.UnlockInvestments(products);
        }
    }
}
