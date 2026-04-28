/// <summary>
/// Periyodik değişiklik tick'inde fırlatılacak action ID'leri.
/// UI tarafı PeriodicChangeManager.OnPeriodicChangeTick event'ini dinleyerek bu action'ları yakalar.
/// Yeni action eklerken mevcut sıralamayı bozma (sadece sona ekle).
/// </summary>
public enum PeriodicChangeAction
{
    None = 0,
    KadinaHediyeAlindi = 1,
    KadinaIyiGecelerMesajiAtildi = 2,
}
