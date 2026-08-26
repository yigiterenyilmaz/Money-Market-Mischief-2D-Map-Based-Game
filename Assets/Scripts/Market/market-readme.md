# Borsa Manipülasyonu (a20 — Finance/Market)

`a20` alt ağacının çalışan parçaları ve eksikleri. Alt ağaç şu şekilde:

```
a20  Profesyonel danışmanlık / yatırım tavsiyesi vermek   → kalıcı danışmanlık geliri
├── a21  Hileli alım satım botu                           → her mum kapanışında kâr + şüphe
├── a22  Bilgi manipülasyonu / yalan bilanço              → AKTİF: piyasaya pump enjekte eder
│   └── a23  Formasyon manipülasyonu                      → AKTİF: istenen formasyonu çizdirir
└── a24  Şirketten insider                                → formasyonun YÖNÜNÜ önceden sızdırır
    └── a25  Devletten insider                            → ad + büyüklük + formasyon tutacak mı
```

## Sistem sınırları

Piyasanın kendisi **bu klasörde değil**: mum simülasyonu ve 38 formasyon
`Assets/Scripts/UI/Investment/` altındadır (bkz. `chart-pattern-readme.md`).
Elle alım satım ise `Assets/Scripts/Trading/TradingSystem.cs`'tedir ve **a11 (Crypto)**
tarafından açılır.

Bu klasör sadece o piyasanın üstündeki manipülasyon katmanını taşır:

| Dosya | Sorumluluk |
|---|---|
| `StockMarketSystem.cs` | Bot, formasyon enjeksiyonu, insider — mantık |
| `MarketIntel.cs` | Formasyon Id → Türkçe ad / yön / büyüklük tablosu |
| `MarketIntelUI.cs` | İpucu şeridi — sunum |

Bağımlılık tek yönlüdür: bu sistem grafiği tanır, grafik bu sistemi tanımaz.
`CandlestickChart` yalnızca `CandleClosed`, `PatternActivated` event'lerini ve
`ForcePattern(id)` çağrısını dışarı açar.

## Sahne kurulumu — YAPILMADAN HİÇBİRİ ÇALIŞMAZ

1. `StockMarketSystem` bileşenini `Managers` objesine ekle.
   `chart` alanını boş bırakabilirsin, sahnede `CandlestickChart` aranır.
2. `MarketIntelUI` bileşenini bir Canvas'ın altındaki boş bir objeye ekle
   (ipucu şeridini kendisi kurar).

`StockMarketSystem` **paylaşılan** `Managers` objesine takıldığı için tekilleştirmede
`Destroy(this)` kullanır, `Destroy(gameObject)` DEĞİL — aksi halde objedeki tüm
yöneticileri birlikte siler.

## YARIM olan / bilinmesi gerekenler

**Bot piyasadan bağımsız kâr yazar.** `a21` botu `TradingSystem` üzerinden gerçek pozisyon
açmaz; kendi sermaye havuzunu işletir ve doğrudan `Wealth`'e yazar. Bunun sebebi
`TradingSystem`'in **tek birikmiş pozisyon** modeli olması: bot oyuncunun pozisyonuna
dokunsaydı elle alım satım bozulurdu. Sonuç: bot ile oyuncunun kendi işlemleri aynı
grafiğe bakar ama birbirini etkilemez.

**Manipülasyon ve insider tek başına para kazandırmaz.** `a22`/`a23` grafiği oynatır,
`a24`/`a25` bilgi verir; bunları paraya çevirmek için ya `a21` botu ya da `a11` ile açılan
elle alım satım paneli gerekir. `a20` alt ağacının kendi başına satın alınması ekonomik
olarak anlamlı değildir — bu bilinçli bir zincir bağımlılığıdır, ama **a11 alınmadıysa
oyuncu grafiği hiç göremez** (panel butonu `TradingSystem.IsUnlocked`'a bağlı).
Tasarımın onaylanması gereken nokta burasıdır.

**İpucu, formasyonun İLK mumundan önce gelir** — `PatternScheduler` formasyonu bir mum
kapanışında seçer, ilk pattern mumu ondan sonra açılır. Yani süre avantajı gerçektir.
Ama `a24`'ün verdiği yön, formasyonun **nihai çözülüş** yönüdür: Omuz-Baş-Omuz "AŞAĞI"
der, oysa fiyat önce 30+ mum boyunca yükselir. Ham yön bilgisi tek başına yanıltıcı
olabilir; `a25`'in ad + büyüklük bilgisi bu yüzden belirgin biçimde daha değerli.
Oynanışta `a24` fazla zayıf gelirse ilk çare ipucuna faz bilgisi eklemektir.

**Uydurulmuş sayılar.** Aşağıdakilerin hiçbiri tasarım notundan gelmiyor, dengelenmeleri
gerekir:

| Yer | Değer |
|---|---|
| `a20` danışmanlık geliri | 25 / sn (sönümsüz) |
| `a21` bot sermayesi / verimi | 50.000 / %60 |
| bot şüphesi | mum başına 0.05 (≈ saatte 30 şüphe) |
| `a22` şüphe bedeli / bekleme | 5 / 180 sn |
| `a23` şüphe bedeli / bekleme | 8 / 300 sn |

Skill maliyetleri ağaç genelindeki karara uyularak `0` bırakıldı.
