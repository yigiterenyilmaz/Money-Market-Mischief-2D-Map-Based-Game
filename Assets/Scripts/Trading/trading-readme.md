# Crypto Alt Ağacı (a11) — Trade, Coin Çıkarma, Tahmin Piyasası

`a11` alt ağacının (Finance/Crypto) tamamı bu klasörde. Ağaç şu şekilde:

```
a11  trade yapmayı açar genel olarak      → TradingSystem (mum grafiğinden al/sat)
 ├── a14  feed tabanlı scam coin           → CoinLaunchSystem (scam)
 ├── a15  sidik yarışı                     → PriceWarSystem (rakiple güç yarışı)
 └── a16  borsa kurma pasif gelir          → PermanentPassiveIncomeEffect
      ├── a17  polymarket                  → PredictionMarketSystem
      └── a18  kendi legal coinin          → CoinLaunchSystem (legal)
           └── a19  flashcrash             → ForceChartPatternEffect (a20'nin sınıfı)
```

**Trade paneli a11 olmadan açılmaz.** `CandlestickChart.requireTradingSkill` (varsayılan
açık) hem panel açma butonunu gizler hem `SetPanelOpen(true)`'yu reddeder. Geliştirme
sırasında skill'siz denemek için Inspector'dan kapatılabilir. Sahnede `TradingSystem`
yoksa panel kalıcı olarak kilitli kalır — skill'i açacak bir şey olmadığı için.

Projede zaten çalışan mum grafiği (`UI/Investment/CandlestickChart.cs` + 38 formasyonluk
simülasyon) tamamen görseldi: para yoktu, al/sat yoktu. `a11` o grafiği gerçek bir trade
ekranına çevirir; alt dallar üstüne kendi mekaniklerini koyar.

---

## Parçalar

| Dosya | Görev | Skill |
|---|---|---|
| `TradingSystem.cs` | Kilit, pozisyon, al/sat mantığı | a11 |
| `TradingPanelUI.cs` | Grafiğin altındaki al/sat şeridi | a11 |
| `CoinLaunchSystem.cs` | Oyuncunun çıkardığı coin'ler (scam + legal) | a14, a18 |
| `CoinPanelUI.cs` | Trade panelinin sağ üstündeki coin kartı + BOŞALT butonu | a14, a18 |
| `PriceWarSystem.cs` | Rakiple güç yarışı: baskı, aşınma, çekilme | a15 |
| `PriceWarUI.cs` | Trade panelinin sol üstündeki savaş şeridi | a15 |
| `PredictionMarketSystem.cs` | Evet/hayır soruları, bahis, sonuçlandırma | a17 |
| `PredictionMarketUI.cs` | Soru listesi ekranı | a17 |
| `TradeUIBuilder.cs` | Panellerin ortak legacy-UI tuğlaları | — |

Effect sınıfları `Skills/Effects/` altında: `UnlockTradingEffect`, `OpenTradingPanelEffect`,
`LaunchScamCoinEffect`, `LaunchLegalCoinEffect`, `UnlockPriceWarEffect`, `StartPriceWarEffect`,
`UnlockPredictionMarketEffect`, `OpenPredictionMarketEffect`.

`CandlestickChart.cs` içine yalnızca ekleme yapıldı: `CurrentPrice`, `IsPanelOpen`,
`SetPanelOpen(bool)`, `SetPanelButtonVisible(bool)`. Mevcut davranış değişmedi.

---

## Mekanikler

### a11 — trade (TradingSystem)
Tek varlık, tek birikmiş pozisyon. Ortalama maliyet ayrı tutulmaz; `costBasis` havuzundan
türetilir, böylece kısmi satışta yuvarlama hatası birikmez.
Kâr = `adet × güncel fiyat − costBasis`. Fiyat panel kapalıyken de akar — pozisyonu açık
bırakıp haritaya dönmek bilinçli olarak risklidir.

### a14 — scam coin (CoinLaunchSystem)
Coin'in fiyatı **feed'e** bağlıdır: sosyal medyada `StockMarket` konulu her post hype'ı
artırır, hype fiyatı şişirir (en fazla 12×). Hype sürekli söner; beslenmezse balon iner.
Hype ne kadar yüksekse **kendiliğinden çökme riski** o kadar yüksektir — çökerse elde
hiçbir şey kalmaz. Oyuncu doğru anda **BOŞALT (rug pull)** der: piyasa değeri cebe girer,
şüphe hype ile orantılı yükselir.

Bu, c ağacındaki feed override skill'leriyle doğrudan sinerji kurar: feed'i piyasaya
kilitleyen oyuncu hype'ı kendi eliyle şişirir.

Kart trade panelinin içinde durur (HUD'da değil): coin de bir piyasa aracı ve oyuncu onu
fiyat hareketiyle birlikte görüyor. Bedeli, boşaltma kararının paneli açmayı gerektirmesi.

### a15 — fiyat savaşı (PriceWarSystem)

Asset'teki not ("sidik yarışı") tek başına uygulanabilir değildi; tasarım kullanıcı
tarafından sözlü olarak verildi ve kaynağı burada saklanıyor:

> Sen bi ürünün fiyatını alarak veya satarak değiştirmeye çalışıyorsun. Mesela sata sata
> düşürmeye çalışıyorsun. Başkası da o anda giriyor sana karşı almaya başlıyor. O anda ya
> gücünü kırana kadar satmaya devam ediyorsun ya da çekiliyorsun.

Oyuncu fiyatı bir yöne iter; karşı tarafta ters yöne basan bir rakip vardır. Her baskı
para yakar, rakibin gücünü aşındırır ve bir öncekinden pahalıdır. Rakip aralıksız
toparlanır, yani duraklamak zemin kaybettirir.

**Rakibin gücü gizlidir** — oyuncu yalnızca bulanık bir cümle görür ("rakip sağlam
duruyor" → "kırılmak üzere"). Bilinen bir sayıyı tüketmek karar değil hesap olurdu;
oyunun tamamı bu belirsizliğin üstünde duruyor. İlk baskı yönü kilitler.

Kırarsan yaktığın paranın 1,8 katı geri döner, şüphe artar ve grafik senin yönünde sert
bir harekete geçer (`D1_Pump` / `D2_Dump`). Çekilirsen yaktığın gider.

### a16 — borsa kurma
`PermanentPassiveIncomeEffect`, saniyede 40. Sönmeyen gelir.

### a17 — tahmin piyasası
Ekranda 3 açık evet/hayır sorusu durur. Katsayı sorunun gerçek olasılığından türetilir,
üstüne %5 ev payı bindirilir. Bir soruya yalnızca bir kez oynanır. Süreler panel kapalıyken
de akar ve sonuçlanır.

### a18 — legal coin
Tek seferlik, kalıcı. Fiyat yavaşça tavana (3×) doğru büyür; her 10 saniyede fiyatla
orantılı para ve az miktarda itibar üretir. Çökmez, boşaltılmaz — scam coin'in dürüst
karşılığı.

### a19 — flash crash
`a20` alt ağacının `ForceChartPatternEffect` sınıfını kullanır (kendi kopyası yazılmadı):
grafiğe `D2_Dump` formasyonunu zorlar, 10 şüphe öder, 240 sn bekleme. Piyasada zaten bir
formasyon işliyorsa **başarısız olur** — bu `StockMarketSystem.ForcePattern`'in tasarım
kararıdır, grafiğin tek formasyon hattı var.

---

## SAHNE BAĞLAMA (bu yapılmadan hiçbir şey çalışmaz)

`enesyeni.unity` sahnesinde `Managers` objesine şu bileşenleri ekle:

1. **`TradingSystem`** — `chart` alanına sahnedeki `CandlestickChart`'ı ver (boş bırakılırsa aranır)
2. **`TradingPanelUI`**
3. **`CoinLaunchSystem`**
4. **`CoinPanelUI`**
5. **`PriceWarSystem`**
6. **`PriceWarUI`**
7. **`PredictionMarketSystem`**
8. **`PredictionMarketUI`**

`CoinPanelUI` ve `PriceWarUI` kendilerini `CandlestickChart.investmentPanel`'in içine
kurar; o alan Inspector'da atanmış olmalı.

Hepsinin `Awake`'i paylaşımlı `Managers` objesinde güvenli olacak şekilde `Destroy(this)`
kullanır — `Destroy(gameObject)` oradaki tüm manager'ları silerdi.

`a19` ayrıca sahnede **`StockMarketSystem`** ister (a20 alt ağacının bileşeni).

---

## Uydurulan sayılar (hepsi Inspector'dan değiştirilebilir)

Kullanıcı denge sayısı vermediği için aşağıdakiler seçildi. Ölçek `a37`'nin
`incomePerSecond: 30` ve `a34/a36/a39`'un `wealthPerTick: 150/600/2000` değerlerine bakılarak
tutturuldu.

| Değer | Seçilen | Not |
|---|---|---|
| `a16` gelir | 40/sn | `a37` 30/sn ile aynı büyüklük sınıfı |
| scam coin çıkış değeri | 10.000 (0,02 × 500.000) | hype 1'de 120.000'e çıkar |
| hype/post | +0,05 | ~20 post tam hype |
| hype sönümü | 0,02/sn | beslenmezse ~50 sn'de sıfırlanır |
| çökme riski | hype 1'de %1/sn | tam hype'ta yarılanma ~70 sn |
| rug pull şüphesi | 6 + 14 × hype | şüphe 100'de oyun biter |
| legal coin geliri | 15/sn → 45/sn | fiyat 1×'ten 3×'e çıktıkça |
| legal coin itibarı | +0,1 / 10 sn | dakikada +0,6 |
| tahmin piyasası ev payı | %5 | |
| tahmin soruları | 7 adet Türkçe soru | **içerik uyduruldu**, Inspector'dan düzenlenir |

`a11`'in `cost` alanı ağacın geri kalanı gibi **0** bırakıldı.

| fiyat savaşı — rakip gücü | 4.000 – 14.000 | ~3-8 baskı |
| fiyat savaşı — ilk baskı | 500, her seferinde %15 zam | |
| fiyat savaşı — zafer | yakılanın 1,8 katı + 8 şüphe | |

---

## Açık uçlar

- **Tek varlık.** Grafik tek fiyat serisi simüle ediyor; coin seçimi yok. Oyuncunun kendi
  coin'leri (a14/a18) grafiğe çizilmiyor, kendi kartlarında yaşıyor. Çoklu varlık istenirse
  `CandlestickChart` varlık başına seri tutacak şekilde genişletilmeli.
- **Kaldıraç / açığa satış yok.** `a19` flash crash şu an yalnızca "dipten al" oynanışı
  veriyor; short pozisyon olsaydı çok daha güçlü olurdu.
- **Tahmin soruları oyun durumuna bağlı değil.** Sonuçlar sabit olasılıkla atılıyor;
  gerçek şüphe/seçim/olay durumuna bağlanabilir.
- **Kayıt/yükleme yok** — projenin genelinde de yok.

## a20 (Market) ile ilişki

`StockMarketSystem` (a20) aynı grafiğin üstünde çalışıyor ve kendi readme'sinde
"oyuncunun eliyle alım satım yapabildiği bir ekran YOK" diye not düşmüştü — `a11`'in trade
şeridi tam olarak o boşluğu dolduruyor. İki sistem çakışmaz: a20 grafiği manipüle eder ve
bot/insider katmanını yönetir, a11 oyuncunun elle pozisyon almasını sağlar.
