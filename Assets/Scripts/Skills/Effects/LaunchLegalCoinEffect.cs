using UnityEngine;

/// <summary>
/// a18 "kendi legal coinini çıkarma" — tek seferlik, kalıcı bir yasal coin listeler.
///
/// Scam coin'in aksine pasif efekttir: skill alınır alınmaz coin doğar, çökmez,
/// boşaltılmaz. Yavaşça değerlenir, her tick para ve az miktarda itibar üretir.
/// </summary>
[System.Serializable]
public class LaunchLegalCoinEffect : SkillEffect
{
    public override void Apply()
    {
        if (CoinLaunchSystem.Instance == null)
        {
            Debug.LogWarning("[LaunchLegalCoinEffect] CoinLaunchSystem sahnede yok — coin çıkarılamadı.");
            return;
        }

        CoinLaunchSystem.Instance.LaunchLegalCoin();
    }
}
