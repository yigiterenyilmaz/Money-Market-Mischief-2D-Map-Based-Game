# Chart Pattern Sistemi

Trading grafiğinde rastgele dolaşan bir fiyat üzerine, gerçek dünyadaki klasik trader formasyonlarını **planlı** olarak enjekte eden sistem. Sistem reaktif değil **proaktif** çalışır: önce "şimdi şu pattern'i yapacağım" diye karar verir, sonra mum mum o pattern'i inşa eder, sonunda resolution (kırılım/dönüş) oynatır.

Toplam **38 pattern** var: A (16 klasik formasyon), B (2 Wyckoff), C (14 mum pattern'i), D (6 price action). Pattern aktif değilken `NoiseDriver` rastgele dolaşma üretir.

---

## 1. Sistem Nasıl Çalışıyor

Her **6 saniyede** bir mum kapanır ve yeni mum açılır. Pattern aktifse o mumun OHLC'si (open/high/low/close) pattern'in fazına göre üretilir, fiyat 6 saniye boyunca o yolu takip eder.

### Akış
1. **Mum kapanışında** scheduler kontrol eder: aktif pattern var mı?
2. **Pattern aktifse** ilerlet → bittiyse kapat
3. **Pattern yoksa** idle counter++. 5-15 mum geçtiyse:
   - MarketState'in context'ine bak (UpTrend / DownTrend / Sideways / NewUpTrend / NewDownTrend / HighVolatility)
   - Context'e uyan pattern adaylarını topla
   - Ağırlıklı rastgele bir pattern seç → spawn

### Pattern context'i nasıl belirleniyor
Son 15 muma bakar:
- Yeşil mum oranı + net fiyat değişimi → momentum (-1 ile +1 arası)
- Momentum > 0.5 + ardışık 10+ yeşil → **UpTrend**, daha kısa → **NewUpTrend**
- Momentum < -0.5 → **DownTrend** / **NewDownTrend**
- Volatilite yüksek (range/ortalama > %4) → **HighVolatility**
- Aksi halde → **Sideways**

### Volatility Multiplier
Inspector'daki `volatilityMultiplier` (0.5 - 2.0) tüm pattern % değerlerini ölçekler. 0.5 = sakin (H&S başı %4 olur), 1.0 = standart, 2.0 = agresif (H&S başı %16).

### Failure Logic (sadece bazı A patternlerinde)
Bazı patternler ~%10-15 ihtimalle "başarısız" olur — beklenen yön yerine ters yöne giden bir resolution oynanır. Trader gerçekliğini taklit eder; her pattern garanti değil.

---

## 2. A Grubu — Klasik Chart Formasyonları (16 pattern)

### A1 — Head and Shoulders (Omuz-Baş-Omuz)
**Bearish reversal**. Yükseliş trendinin sonunda görülür → düşüşe döner.
**Tetikleyici**: UpTrend (uzun yükseliş)  •  **Toplam süre**: 30-46 mum  •  **Failure**: %12

**Hareket**:
1. **Setup** (4-6 mum): Fiyat hafif %1 yükselir
2. **Sol omuz yukarı** (3-5 mum): %4'e çıkar
3. **Sol omuz aşağı** (3-5 mum): %0'a iner (neckline)
4. **Baş yukarı** (4-6 mum): %8'e fırlar, son mum doji veya shooting star (zirve tereddüdü)
5. **Baş aşağı** (4-6 mum): %0'a tekrar iner
6. **Sağ omuz yukarı** (3-5 mum): %4'e çıkar (ama momentum az)
7. **Sağ omuz aşağı** (3-5 mum): %-1'e iner — neckline kırılır
8. **Breakdown** (6-10 mum): %-8'e çöküş, ilk mum büyük kırmızı marubozu

**Failed (%12)**: Breakdown yerine **+%9 rally** olur (sürpriz yükseliş).

---

### A2 — Inverse Head and Shoulders (Ters Omuz-Baş-Omuz)
**Bullish reversal**. A1'in tersi — düşüş trendinin sonunda görülür → yükselişe döner.
**Tetikleyici**: DownTrend  •  **Süre**: 30-46 mum  •  **Failure**: %12

**Hareket**: Setup -%1 → sol omuz dip -%4 → -%0 → bas dip -%8 (son mum hammer/doji) → -%0 → sağ omuz dip -%4 → +%1 → **breakout +%8** (yeşil marubozu).
**Failed**: Breakout yerine -%5 selloff.

---

### A3 — Double Top (M Formasyonu)
**Bearish reversal**. İki tepeye çıkar, ikincisini geçemez, çöker.
**Tetikleyici**: UpTrend  •  **Süre**: 18-32 mum  •  **Failure**: %10

**Hareket**:
1. İlk tepe yukarı (3-5 mum): %5'e
2. İlk tepe aşağı (3-5 mum): %1'e (vadi)
3. İkinci tepe yukarı (3-5 mum): %5'e (±%0.3 noise — aynı seviye), son mum küçük (rejection)
4. İkinci tepe aşağı (3-5 mum): %1'e
5. **Breakdown** (6-12 mum): -%3'e (vadi kırılır), büyük kırmızı marubozu

**Failed (%10)**: Breakdown yerine +%8 rally.

---

### A4 — Double Bottom (W Formasyonu)
**Bullish reversal**. A3'ün tersi.
**Tetikleyici**: DownTrend  •  **Süre**: 18-32 mum  •  **Failure**: %10

**Hareket**: Dip -%5 → -%1 → ikinci dip -%5 (hammer) → -%1 → **breakout +%3** (yeşil marubozu).
**Failed**: -%8 selloff.

---

### A5 — Triple Top
**Bearish reversal**. A3 ama 3 tepe.
**Tetikleyici**: UpTrend  •  **Süre**: 25-45 mum  •  **Failure**: %10

**Hareket**: 3 kere %5'e çıkar, aralarda %1 vadi → 3. tepede rejection → **breakdown -%3**.
**Failed**: +%8 rally.

---

### A5 — Triple Bottom
**Bullish reversal**. Triple Top'un tersi.
**Tetikleyici**: DownTrend  •  **Süre**: 25-45 mum  •  **Failure**: %10

**Hareket**: 3 kere -%5'e iner, aralarda -%1 → 3. dipte hammer → **breakout +%3**.
**Failed**: -%8 selloff.

---

### A6 — Ascending Triangle (Yükselen Üçgen)
**Bullish continuation**. Yatay direnç + yükselen dipler → yukarı kırılım.
**Tetikleyici**: NewUpTrend / UpTrend  •  **Süre**: 18-28 mum  •  **Failure**: %15

**Hareket**:
1. 1. dokunuş aşağı (2-3 mum): %0'a (P0)
2. 1. dokunuş yukarı (2-3 mum): %4'e direnç, son mum doji (rejection)
3. 2. dokunuş aşağı: %1'e (dip yükseldi)
4. 2. dokunuş yukarı: %4'e tekrar
5. 3. dokunuş aşağı: %2'ye (dip daha da yükseldi)
6. 3. dokunuş yukarı: %4'e (sıkışma)
7. **Breakout** (5-10 mum): %8'e fırlama, yeşil marubozu

**Failed (%15)**: Breakout yerine -%4'e iniş (P0+0%).

---

### A7 — Descending Triangle (İnen Üçgen)
**Bearish continuation**. A6'nın tersi: yatay support + alçalan tepeler.
**Tetikleyici**: NewDownTrend / DownTrend  •  **Süre**: 18-28 mum  •  **Failure**: %15

**Hareket**: 3 kere -%4 support'a iner, tepeler %0 → -%1 → -%2 olarak alçalır → **breakdown -%8**.
**Failed**: +%4 rally.

---

### A8 — Symmetrical Triangle (Simetrik Üçgen)
**Neutral** (yön trende göre). Tepeler alçalır, dipler yükselir → daralma → kırılım.
**Tetikleyici**: NewUpTrend veya NewDownTrend  •  **Süre**: 18-28 mum

**Hareket**: %4 → -%4 → %2.5 → -%2.5 → %1 → -%1 (sıkışma) → **breakout/breakdown ±%6** (trende göre yön).

---

### A9 — Bull Flag (Boğa Bayrağı)
**Bullish continuation**. Hızlı yükseliş + dinlenme + devam.
**Tetikleyici**: NewUpTrend  •  **Süre**: 12-22 mum  •  **Failure**: %15

**Hareket**:
1. **Flagpole** (3-6 mum): peş peşe büyük yeşillerle %6'ya fırlama (yeşil marubozu açılış)
2. **Flag** (6-12 mum): %4'e hafif geri çekilme, küçük gövdeler (paralel kanal aşağı)
3. **Breakout** (4-8 mum): %10'a sıçrama, yeşil marubozu

**Failed (%15)**: Breakout yerine %0'a düşüş (-%4).

---

### A10 — Bear Flag (Ayı Bayrağı)
**Bearish continuation**. A9'un tersi.
**Tetikleyici**: NewDownTrend  •  **Süre**: 12-22 mum  •  **Failure**: %15

**Hareket**: -%6 flagpole → -%4 flag → **breakdown -%10**.
**Failed**: %0'a yükseliş (+%4).

---

### A11 — Pennant (Flama)
**Bull veya bear continuation** (init'te trend yönüne göre seçer).
**Tetikleyici**: NewUpTrend veya NewDownTrend  •  **Süre**: 13-24 mum

**Hareket**:
1. **Pole** (3-6 mum): trend yönünde ±%6 hızlı momentum (marubozu açılış)
2. **Pennant** (6-10 mum): pole hedefi etrafında ±%5'te sıkışma, mikro mumlar
3. **Breakout** (4-8 mum): trend yönünde ±%10

---

### A12 — Rising Wedge (Yükselen Kama)
**Bearish reversal**. Hem dipler hem tepeler yükselir ama tepe eğimi az → daralır → düşer.
**Tetikleyici**: UpTrend  •  **Süre**: 18-28 mum

**Hareket**: 3 kere yukarı dokunuşlar (%3 → %3.5 → %3.8), aralarda dipler yükselir (%0 → %1 → %1.8) → **breakdown -%4**.

---

### A13 — Falling Wedge (Düşen Kama)
**Bullish reversal**. A12'nin tersi.
**Tetikleyici**: DownTrend  •  **Süre**: 18-28 mum

**Hareket**: 3 kere aşağı dokunuşlar (-%3 → -%3.5 → -%3.8), aralarda tepeler alçalır (%0 → -%1 → -%1.8) → **breakout +%4**.

---

### A14 — Cup and Handle (Fincan ve Kulp)
**Bullish continuation**. Uzun yumuşak U-şekli + küçük pullback + breakout.
**Tetikleyici**: Sideways / UpTrend / NewUpTrend  •  **Süre**: 40-70 mum

**Hareket**:
1. **Sol kenar** (4-6 mum): %0'da yatay
2. **Fincana iniş** (8-14 mum): yumuşak -%8'e iniş, sürekli orta-küçük kırmızılar
3. **Fincan dibi** (5-8 mum): -%8'de mikro mumlar (sakin)
4. **Fincandan çıkış** (8-14 mum): yumuşak %0'a yükseliş
5. **Kulp** (4-8 mum): -%2'ye küçük geri çekilme
6. **Breakout** (5-10 mum): %8'e fırlama, yeşil marubozu

---

### A15 — Rectangle (Range / Yatay Kanal)
**Neutral** (yön rastgele). Support ve resistance arası tekrarlı dokunuş.
**Tetikleyici**: Sideways  •  **Süre**: 20-40 mum

**Hareket**: 3 kere %2'ye çıkış, 3 kere -%2'ye iniş → **breakout ±%4** (yön rastgele).

---

## 3. B Grubu — Wyckoff (Profesyonel Akümülasyon/Distribüsyon)

Wyckoff metodu **uzun**, dramatik kurum-tipi formasyonlardır. Nadiren tetiklenir (weight 0.5).

### B1 — Wyckoff Accumulation (Akümülasyon)
**Bullish setup** — uzun düşüşten sonra büyük yükseliş hazırlığı.
**Tetikleyici**: DownTrend (uzun düşüş)  •  **Süre**: 60-100 mum

**Hareket**:
1. **PS — Preliminary Support** (5-8 mum): -%3'e düşüş yavaşlar
2. **SC — Selling Climax** (3-5 mum): -%11'e panik satış (büyük kırmızı marubozular)
3. **AR — Automatic Rally** (5-10 mum): -%3'e hızlı toparlanma (büyük yeşiller)
4. **ST — Secondary Test** (8-15 mum): -%9'a yumuşak iniş (volume azalır)
5. **SPRING** (2-3 mum): -%9'a sahte breakdown — uzun alt fitilli yeşil mum (false breakdown + recovery)
6. **TEST** (4-7 mum): -%7'de küçük karışık mumlar
7. **SOS — Sign of Strength** (5-10 mum): %1'e büyük yeşil marubozular
8. **LPS — Last Point of Support** (3-5 mum): -%1'e küçük pullback
9. **MARKUP** (10-15 mum): %10'a büyük yükseliş (yeni uptrend başlar)

---

### B2 — Wyckoff Distribution (Distribüsyon)
**Bearish setup** — uzun yükselişten sonra büyük çöküş hazırlığı. B1'in tersi.
**Tetikleyici**: UpTrend (uzun yükseliş)  •  **Süre**: 60-100 mum

**Hareket**: PSY +%3 → BC +%11 (FOMO) → AR +%3 → ST +%9 → **UPTHRUST** +%9 (uzun üst fitilli kırmızı, false breakout) → TEST +%7 → SOW -%1 (büyük kırmızılar) → LPSY +%1 → **MARKDOWN -%10** (büyük çöküş).

---

## 4. C Grubu — Mum (Candlestick) Pattern'leri

1-3 mumluk **mini-event**'ler. Çok sık görülür (weight 5).

### C1 — Hammer (Çekiç)
**Bullish reversal sinyali**. Düşüş trendinde tek mum: küçük gövde + uzun alt fitil → "satıcılar dipte alıcılar tarafından geri itildi".
**Tetikleyici**: DownTrend / NewDownTrend  •  **Süre**: 1 mum

**Şekil**: Body %0.10-0.30 yeşil, alt fitil %0.60-1.20 (gövdenin ~5 katı), üst fitil yok.

---

### C2 — Inverted Hammer (Ters Çekiç)
**Bullish reversal**. Düşüş sonu, body küçük yeşil + uzun üst fitil + mikro alt fitil.
**Tetikleyici**: DownTrend  •  **Süre**: 1 mum

---

### C3 — Shooting Star (Kayan Yıldız)
**Bearish reversal**. Yükseliş sonu, body küçük kırmızı + uzun üst fitil + mikro alt fitil → "alıcılar tepede satıcılar tarafından düşürüldü".
**Tetikleyici**: UpTrend  •  **Süre**: 1 mum

---

### C4 — Hanging Man (Asılan Adam)
**Bearish reversal**. Yükseliş tepesinde hammer şekli (uzun alt fitil) ama kırmızı.
**Tetikleyici**: UpTrend  •  **Süre**: 1 mum

---

### C5 — Doji (Kararsızlık)
**Reversal sinyali** — gerçek trading gibi davranır:
- **UpTrend tepesinde** veya **DownTrend dibinde** %65 ihtimalle 2 mumlu pattern: doji + ters yönde confirmation (orta gövdeli mum)
- Diğer durumlarda %35 + tüm sideways/HighVolatility'de tek mum kalır

**Şekil**: Body ≤ %0.05 (open ≈ close), iki yanda %0.30-0.60 fitil. Confirmation mum'u (varsa) trend tersine %0.40-0.80 gövde.

---

### C6 — Bullish Engulfing
**Bullish reversal**. 2 mum: küçük kırmızı + onu yutan büyük yeşil.
**Tetikleyici**: DownTrend  •  **Süre**: 2 mum

**Şekil**:
- 1. mum: kırmızı %0.4-0.6 gövde
- 2. mum: yeşil %0.8-1.2 gövde, **2. mum açılışı 1. mumun close'unun altında, kapanışı open'ın üstünde** (1.in body'sini tamamen örter)

---

### C7 — Bearish Engulfing
**Bearish reversal**. 2 mum: küçük yeşil + onu yutan büyük kırmızı.
**Tetikleyici**: UpTrend  •  **Süre**: 2 mum

---

### C8 — Morning Star (Sabah Yıldızı)
**Bullish reversal**. 3 mumlu klasik dönüş.
**Tetikleyici**: DownTrend  •  **Süre**: 3 mum

**Şekil**:
1. **Mum 1**: kırmızı %1.0-1.4 gövde (downtrend'in son büyük kırmızısı)
2. **Mum 2**: doji veya küçük gövde %0.05-0.15, **1. mumun altında küçük gap-down** (bıkkınlık)
3. **Mum 3**: yeşil %1.0-1.4, **2. mumun üstünde gap-up**, 1. mumun body ortasına kadar çıkar (dönüş onayı)

---

### C9 — Evening Star (Akşam Yıldızı)
**Bearish reversal**. C8'in tersi: yeşil + doji + büyük kırmızı.
**Tetikleyici**: UpTrend  •  **Süre**: 3 mum

---

### C10 — Three White Soldiers (Üç Beyaz Asker)
**Bullish momentum**. 3 ardışık güçlü yeşil → "alıcılar geliyor".
**Tetikleyici**: DownTrend  •  **Süre**: 3 mum

**Şekil**: 3 yeşil mum, her birinin body'si %0.7-0.9. **Her mum öncekinin body ortasında açılır, öncekinin close'unun üstünde kapanır**. Üst fitiller mikro (alıcı baskın).

---

### C11 — Three Black Crows (Üç Siyah Karga)
**Bearish momentum**. 3 ardışık güçlü kırmızı → "satıcılar geliyor".
**Tetikleyici**: UpTrend  •  **Süre**: 3 mum

---

### C12 — Marubozu
**Güçlü momentum mum'u**. Body %1.5-2.5, hiç fitil yok.
**Tetikleyici**: UpTrend / DownTrend (trend yönünde renk)  •  **Süre**: 1 mum

---

### C13 — Bullish Harami
**Bullish zayıflama sinyali**. 2 mum: büyük kırmızı + içinde küçük yeşil → "satış momentumu zayıflıyor".
**Tetikleyici**: DownTrend  •  **Süre**: 2 mum

**Şekil**:
- 1. mum: kırmızı %1.0-1.4 (downtrend'in büyük mum'u)
- 2. mum: yeşil %0.2-0.4, **body'si tamamen 1. mumun body'si içinde** (üstte ve altta dışına çıkmaz)

---

### C14 — Bearish Harami
**Bearish zayıflama**. C13'ün tersi: büyük yeşil + içinde küçük kırmızı.
**Tetikleyici**: UpTrend  •  **Süre**: 2 mum

---

## 5. D Grubu — Price Action (Olaylar)

### D1 — Pump (FOMO Rally)
**Hızlı yükseliş + tepede tereddüt**.
**Tetikleyici**: UpTrend / NewUpTrend / Sideways  •  **Süre**: 5-10 mum

**Hareket**:
1. **Acceleration** (2-3 mum): %3'e momentum yükselişi (orta yeşiller)
2. **Climax** (3-5 mum): %10'a peş peşe büyük yeşiller (FOMO buyer'lar). İlk mum yeşil marubozu.
3. **Exhaustion** (1-2 mum): %11'de doji veya shooting star (zirvede tereddüt — FOMO bitti)

Sonrası: scheduler büyük ihtimalle bir reversal pattern (D2 dump veya A1 H&S) tetikler.

---

### D2 — Dump (Capitulation / Çöküş)
**Hızlı düşüş**. D1'in tersi.
**Tetikleyici**: UpTrend / DownTrend / Sideways  •  **Süre**: 5-10 mum

**Hareket**:
1. **Break** (1-2 mum): -%2 büyük kırmızı marubozu (tetikleyici)
2. **Acceleration** (2-3 mum): -%6 peş peşe büyük kırmızılar (gap-down açılışlar)
3. **Capitulation** (2-4 mum): -%11 çok büyük kırmızı + alt fitil (panik dipte alıcı çıkar)
4. **Bounce** (1-2 mum): -%9'a küçük yeşil (dead cat bounce)

---

### D3 — Liquidity Grab / Stop Hunt
**S/R seviyesini hızla aşıp geri dönüş**. Tek mumda dramatik fitil + sonra reversal.
**Tetikleyici**: HighVolatility  •  **Süre**: 6-11 mum

**Hareket**:
1. **Grab** (1 mum): Aşağı veya yukarı yönde (rastgele) — eğer aşağı: alt fitili %0.6-1.2 uzun, body küçük yeşil (geri dönmüş halde). Aşağı false break + hemen recovery.
2. **Reversal** (5-10 mum): ±%4 ters yönde momentum mumları

---

### D4 — Fakeout (Yalancı Kırılım)
**Yanıltıcı break + ters yönde gerçek hamle**.
**Tetikleyici**: HighVolatility / Sideways  •  **Süre**: 8-14 mum

**Hareket**:
1. **False Break** (2-3 mum): ±%2 sahte kırılım yönünde
2. **Rejection** (1-2 mum): %0'a geri dönüş, uzun fitilli ters yön mum
3. **True Move** (5-9 mum): ∓%5 gerçek yön (ilk mum büyük marubozu)

---

### D5 — Squeeze (Volatilite Sıkışması)
**Uzun süre küçük mumlar + ani patlama**.
**Tetikleyici**: Sideways  •  **Süre**: 12-23 mum

**Hareket**:
1. **Tightening** (8-15 mum): %0 civarında mikro mumlar (range çok dar — alıcı satıcı dengeli)
2. **Explosion** (4-8 mum): ±%5 (yön rastgele), ilk mum büyük marubozu

---

### D6 — Trend Continuation Pullback
**Güçlü trend içinde geri çekilme + devam**.
**Tetikleyici**: NewUpTrend / NewDownTrend / UpTrend / DownTrend  •  **Süre**: 6-12 mum

**Hareket** (trend yönüne göre yön belirler):
1. **Pullback** (3-5 mum): trend tersine ±%2.5 (Fibonacci ~0.5 retracement)
2. **Resumption** (3-7 mum): trend yönünde ±%5 (yeni high/low)

---

## 6. Pattern Ağırlıkları (Sıklık)

Scheduler context'e uyan adaylardan ağırlıklı rastgele seçer. Yüksek ağırlık = sık çıkar.

| Ağırlık | Pattern grubu | Detay |
|---|---|---|
| **5** | C grubu (mum pattern'leri) | C1-C14 — çok sık |
| **4** | D3-D6 küçük price action | LiquidityGrab, Fakeout, Squeeze, Pullback |
| **3** | A6-A11, A15 + A8 | Triangle, Flag, Pennant, Rectangle |
| **2** | A3-A5, A12, A13 | Double/Triple Top/Bottom, Wedge'ler |
| **1** | A1, A2, A14, D1, D2 | H&S, Cup&Handle, Pump, Dump (nadir, dramatik) |
| **0.5** | B1, B2 | Wyckoff (çok nadir) |

Aynı pattern son 3 spawn'da varsa cooldown'a girer (recently used filter).

---

## 7. Mum İçi Animasyon

Bir mum 6 saniye sürer. Pattern mum başında **nihai OHLC**'yi belirler (örn. open=100, high=100.3, low=99.5, close=100.2). Sonra `CandlePathPlayer` 6 saniye boyunca fiyatı bu noktaları gezdirir:

| Karakter | Yol |
|---|---|
| Yeşil generic | open → low (~%30) → high (~%70) → close |
| Kırmızı generic | open → high (~%30) → low (~%70) → close |
| Marubozu | open → close (monoton, fitil yok) |
| Hammer (LongLowerWick) | open → low (~%40) → close |
| Shooting Star (LongUpperWick) | open → high (~%40) → close |
| Doji | high → low → high → close (salınım) |

`Mathf.SmoothStep` ile waypoint'ler arasında yumuşak geçiş. Kullanıcı mum büyürken hammer/shooting star/marubozu şekillerinin oluştuğunu canlı görür.

---

## 8. Failure Mekanizması (Sadece Bazı A Patternlerinde)

Bazı patternler ~%10-15 ihtimalle ters resolution oynatır. `Init` sırasında zar atılır.

| Pattern | Failure | Normal Resolution | Failed Resolution |
|---|---|---|---|
| A1 H&S | %12 | -%8 breakdown | +%9 rally |
| A2 Inv H&S | %12 | +%8 breakout | -%5 selloff |
| A3 Double Top | %10 | -%3 breakdown | +%8 rally |
| A4 Double Bottom | %10 | +%3 breakout | -%8 selloff |
| A5 Triple Top/Bottom | %10 | ±%3 | ±%8 ters |
| A6 Asc Triangle | %15 | +%8 breakout | -%4 (P0+0%) |
| A7 Desc Triangle | %15 | -%8 breakdown | +%4 |
| A9 Bull Flag | %15 | +%10 breakout | -%4 (P0+0%) |
| A10 Bear Flag | %15 | -%10 breakdown | +%4 |

Failed çalıştığında Console log'da phase ismi `FailedRally` / `FailedSelloff` olarak görünür.

---

## 9. Debug ve Test

### Pattern manuel tetikleme
1. Inspector'da `CandlestickChart` bileşenine git
2. **Force Pattern Id** field'ına Id yaz (örn. `C8_MorningStar`, `B1_WyckoffAccumulation`, `D3_LiquidityGrab`)
3. Bileşene sağ-tık → **Force Pattern by Id**

Hızlı kestirmeler: Sağ-tık menüde 4 hazır pattern var (Pump, Doji, AscTri, H&S).

### Console log'lar
- Pattern başında: `[Pattern] {Id} started, P0={x}, totalCandles~{n}`
- Her phase başında: `[Pattern] {Id} phase '{name}' starting, target={y}, duration={n} mum`
- Pattern bitince: `[Pattern] {Id} done`

### Inspector parametreleri
- `volatilityMultiplier` (0.5-2): pattern büyüklüğü çarpanı
- `patternCooldownMin/Max` (5-15): pattern arası bekleme süresi (mum)
- `volatility, trendNoise, trendDecay, maxTrend`: idle (NoiseDriver) random walk parametreleri

---

## 10. Kısa Pattern Id Listesi

A grubu: `A1_HeadAndShoulders`, `A2_InverseHeadAndShoulders`, `A3_DoubleTop`, `A4_DoubleBottom`, `A5_TripleTop`, `A5_TripleBottom`, `A6_AscendingTriangle`, `A7_DescendingTriangle`, `A8_SymmetricalTriangle`, `A9_BullFlag`, `A10_BearFlag`, `A11_Pennant`, `A12_RisingWedge`, `A13_FallingWedge`, `A14_CupAndHandle`, `A15_Rectangle`

B grubu: `B1_WyckoffAccumulation`, `B2_WyckoffDistribution`

C grubu: `C1_Hammer`, `C2_InvertedHammer`, `C3_ShootingStar`, `C4_HangingMan`, `C5_Doji`, `C6_BullishEngulfing`, `C7_BearishEngulfing`, `C8_MorningStar`, `C9_EveningStar`, `C10_ThreeWhiteSoldiers`, `C11_ThreeBlackCrows`, `C12_Marubozu`, `C13_BullishHarami`, `C14_BearishHarami`

D grubu: `D1_Pump`, `D2_Dump`, `D3_LiquidityGrab`, `D4_Fakeout`, `D5_Squeeze`, `D6_ContinuationPullback`
