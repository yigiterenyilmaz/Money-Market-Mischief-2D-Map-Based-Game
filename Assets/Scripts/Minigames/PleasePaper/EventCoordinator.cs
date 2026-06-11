using UnityEngine;

/// <summary>
/// Farklı event sistemlerinin (RandomEvent, PleasePaper, Smuggle vb.) aynı anda
/// event göstermesini engelleyen cooldown sistemi.
/// Tam kilitleme yapmaz — sadece iki event arasında minimum süre bırakır.
/// </summary>
public static class EventCoordinator
{
    private static float lastEventTime = -999f;
    private static float cooldownDuration = 2f; //iki event arası minimum süre (saniye)
    private static string lockOwner; //event kilidi sahibi (null = kilit yok)

    /// <summary>
    /// Event gösterilebilir mi kontrol eder.
    /// Kilit varsa veya cooldown dolmadıysa false döner.
    /// </summary>
    public static bool CanShowEvent()
    {
        if (lockOwner != null) return false;
        return Time.time - lastEventTime >= cooldownDuration;
    }

    /// <summary>
    /// Tüm event'leri kilitle. Kilit sahibi bırakana kadar hiçbir event gösterilemez.
    /// WarForOil zinciri gibi uzun süreçlerde kullanılır.
    /// </summary>
    public static void LockEvents(string owner)
    {
        lockOwner = owner;
    }

    /// <summary>
    /// Event kilidini bırak. Sadece kilit sahibi bırakabilir.
    /// </summary>
    public static void UnlockEvents(string owner)
    {
        if (lockOwner == owner)
            lockOwner = null;
    }

    /// <summary>
    /// Event kilidi aktif mi.
    /// </summary>
    public static bool IsLocked()
    {
        return lockOwner != null;
    }

    /// <summary>
    /// Event gösterildi olarak işaretle. Cooldown sayacını sıfırlar.
    /// Her event popup'ı gösterildiğinde çağrılmalı.
    /// </summary>
    public static void MarkEventShown()
    {
        lastEventTime = Time.time;
    }

    /// <summary>
    /// Cooldown süresini değiştirir (varsayılan 2 saniye).
    /// </summary>
    public static void SetCooldownDuration(float duration)
    {
        cooldownDuration = Mathf.Max(0f, duration);
    }
}
