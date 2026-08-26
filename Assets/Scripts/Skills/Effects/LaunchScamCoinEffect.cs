using UnityEngine;

/// <summary>
/// a14 "feed tabanlı scam coin çıkartma" — piyasaya yeni bir scam coin sürer.
///
/// Skill'in AKTİF yeteneğine bağlanır: her kullanımda yeni bir coin çıkar, oyuncu feed'i
/// besleyip hype'ı şişirir ve balon patlamadan önce boşaltır (kart üzerindeki BOŞALT butonu).
/// Boşaltma kararı skill'e değil ana ekrandaki karta bağlıdır — doğru anı yakalamak
/// oyuncunun işidir.
/// </summary>
[System.Serializable]
public class LaunchScamCoinEffect : SkillEffect
{
    public override void Apply()
    {
        if (CoinLaunchSystem.Instance == null)
        {
            Debug.LogWarning("[LaunchScamCoinEffect] CoinLaunchSystem sahnede yok — coin çıkarılamadı.");
            return;
        }

        CoinLaunchSystem.Instance.LaunchScamCoin();
    }
}
