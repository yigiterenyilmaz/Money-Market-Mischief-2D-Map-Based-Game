public enum StatType
{
    Wealth,
    Suspicion,
    Reputation,         //eski Trust, şüphe artışını modifiye eder
    PoliticalInfluence, //siyasi nüfuz, -100 ile +100 arası, skill verimini etkiler

    //DİKKAT: yeni değerler yalnızca SONA eklenir. Asset'ler statType'ı İNDEKS olarak saklar
    //(statType: 2 gibi); araya ekleme mevcut tüm skill/event asset'lerini sessizce kaydırır.

    /// <summary>
    /// Güven. Riske karşı tampon: yüksek güven, kötü sonuçların etkisini yumuşatır.
    /// Yukarıdaki Reputation'ın "eski Trust" notuyla KARIŞTIRILMAMALI — o ayrı bir stattır.
    ///
    /// YARIM: stat'ın kendisi (biriktirme, tavan, event) GameStatManager'da hazır, ancak
    /// güvenin riski AZALTMA davranışı henüz bağlı değil. Bkz. Assets/Scripts/Stats/trust-system-readme.md
    /// </summary>
    Trust
}
