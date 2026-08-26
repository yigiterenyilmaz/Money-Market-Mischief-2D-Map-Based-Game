using UnityEngine;

/// <summary>
/// a21 "hileli alım satım botu" — borsada oyuncu adına işlem yapan, yönü önceden bildiği için
/// asla zarar etmeyen bot. Açıldıktan sonra her mum kapanışında sermaye × hareket × verim
/// kadar kâr yazar (StockMarketSystem).
///
/// Şüphe bedeli botun kendisinde değil StockMarketSystem'dedir (mum başına), çünkü bedel
/// zamanla akar — tek seferlik bir ödeme değil.
/// </summary>
[System.Serializable]
public class UnlockTradingBotEffect : SkillEffect
{
    [Tooltip("Botun işlettiği sermaye. Kâr bunun yüzdesi olarak hesaplanır.")]
    public float capital = 50000f;

    [Tooltip("Botun mum gövdesinin ne kadarını yakaladığı (0-1). 1 = her hareketi tam yakalar.")]
    [Range(0f, 1f)] public float efficiency = 0.6f;

    public override void Apply()
    {
        if (StockMarketSystem.Instance == null)
        {
            Debug.LogWarning("[UnlockTradingBotEffect] StockMarketSystem sahnede yok — bot açılamadı.");
            return;
        }

        StockMarketSystem.Instance.UnlockTradingBot(capital, efficiency);
    }
}
