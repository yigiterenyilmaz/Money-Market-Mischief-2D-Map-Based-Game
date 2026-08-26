# Medya + Siyaset Ağaçları — Uygulama Notları

B (Medya) ve C (Siyaset) ağaçlarının rüşvet dışı kalan 26 skill'i bağlandı.
Tasarım kaynağı: [politics-media-design.md](../Skills/politics-media-design.md).

**Bu dosyanın asıl işi, hangi sayıların KULLANICIDAN geldiğini hangilerinin UYDURULDUĞUNU
ayırmak.** Tasarım notlarının hiçbirinde sayı yoktu; aşağıdaki her rakam benim koyduğum
başlangıç değeridir ve Inspector'dan değiştirilebilir.

---

## 1. Ne yapıldı, ne yapılmadı

| | Skill | Durum |
|---|---|---|
| **B — Medya** | b1–b15 (15) | ✅ tamamı bağlandı |
| **C — Siyaset** | c1, c2, c3, c5, c7, c8, c13, c14, c15, c16, c21 (11) | ✅ bağlandı |
| **C — atlandı** | c4, c6, c10, c11, c12, c17, c18, c20 (8) | ⏸ rüşvet mekaniğine bağlı |
| **C — yok** | c9, c19 | ❌ asset'i hiç yok |

Atlananların hepsi rüşvet zincirine dayanıyor: `c6/c10/c11/c12` doğrudan rüşvet, `c17` rüşvet
pazarlığını güçlendiriyor, `c4/c18` mafya (tek tanımlı getirisi rüşvet ve öldürme), `c20`
seçimde hile (seçim sistemi yok). Bu düğümlerdeki **sahte aktif yetenekler temizlendi** —
"Manşet Patlatma", "Troll Ordusu", "Karalama Kampanyası" cooldown halkası demosuydu ve gerçek
tasarımla çelişiyordu. Cooldown halkası artık 14 gerçek aktif yetenekle zaten test edilebiliyor.

## 2. Sahneye eklenmesi gereken iki bileşen

**Bunlar eklenmeden hiçbir yeni skill çalışmaz.** `Managers` objesine iki MonoBehaviour:

- `MediaSystem` — `Assets/Scripts/Media/MediaSystem.cs`
- `PoliticsSystem` — `Assets/Scripts/Politics/PoliticsSystem.cs`

İkisi de singleton hazardına karşı `Destroy(this)` kullanıyor (`Destroy(gameObject)` değil),
yani paylaşılan objedeki diğer manager'ları götürmezler. Referans alanları yoktur —
"Add Component" yeter.

Üçüncü bir bileşen isteğe bağlı: `StatHUD` (`Assets/Scripts/UI/StatHUD.cs`) itibar ve şüphe
barlarını ekrana getirir. Kendini koddan kurar; Canvas'ın altında değilse sahnedeki ilk
Canvas'ı bulur, yani paylaşılan `Canvas.prefab`'a dokunmadan herhangi bir objeye eklenebilir.

## 3. Ağaçların nasıl çalıştığı

### Medya = itibar motoru
İtibar bir puan değil, **şüphenin artış hızını belirleyen çarpan** (`GetSuspicionMultiplier`,
itibar 0'da 1.5x → tavanda 0.5x). Şüphe dolunca oyun bittiği için medya ağacı doğrudan
hayatta kalma süresini uzatıyor.

**Erişim (reach)** bütün aktif hamleleri çarpar. Mecralar tek başına hamle yapmaz:

| Mecra | Skill | Erişim | Ek |
|---|---|---:|---|
| Yerel gazete | b2 | +0.25 | itibar kazanımı ×1.05 kalıcı |
| Sosyal medya | b3 | +0.35 | "Kendini Övdür" hamlesi |
| Haber kanalı | b6 | +0.60 | itibar kazanımı ×1.15 kalıcı |
| Ünlüler | b7 | +0.50 | Celebrity konu ağırlığı +0.15 |

Taban 1.0 → hepsi alınınca 2.70. c16 (medyaya kadro) bunu +0.60'a kadar daha büyütür.

**b2/b6/b7 tek başına gözle görülür bir şey yapmaz — çarpan düğümleridir.** Bu bilinçli;
ağacın geri kalanının hepsini birden büyütürler.

### Siyaset = nüfuz + kadro
Siyasi nüfuz da bir puan değil: `GetSkillEfficiencyMultiplier()` onu 0.5x–1.5x arası bir verim
çarpanına çeviriyor ve siyaset hamlelerinin getirisi bununla çarpılıyor — c1'in "çarpan gibi
çalışır" tarifi bu. **Not:** o metot bu iş öncesinde kod tabanında hiçbir yerden çağrılmıyordu;
ilk tüketicisi `PoliticsSystem` oldu.

Kadro kolu: c3/c13 zamanla kadro yetiştirir → c14/c15/c16 kadroyu yerleştirir.
Kaynak varsa ama açık alan yoksa üretim başlamaz (ikisi de gerekli).

## 4. Uydurulan sayılar

Hepsi Inspector'da, hepsi tartışmaya açık.

**Medya hamleleri**

| Hamle | Skill | Bedel | Getiri |
|---|---|---|---|
| Yardım | b4 | 25.000 | +6 itibar × boş arazi oranı × erişim |
| Demeç | b5 | — | +3 itibar × erişim, 25 sn gündem |
| Münazara | b15 | — | kazanınca +10×erişim, kaybedince −6 itibar +2 şüphe |
| Kendini övdür | b3 | — | +4 itibar × erişim, +0.5 şüphe |
| Konuyu dağıt | b8 | — | 45 sn gündem bastırma |
| Propaganda | b9 | — | +5 nüfuz × erişim, +1 şüphe, 60 sn gündem |
| İfşa | b10 | — | +10 nüfuz × erişim, **+6 şüphe**, 90 sn gündem |

Münazara kazanma şansı: `0.5 + 0.25×(itibar oranı) + 0.10×(erişim−1)`.

**Anketler** — hata payı erişime bölünür (erişim büyüdükçe anket isabetlenir):
itibar ±8, şüphe ±5, nüfuz ±6. Şüphe anketinin bedeli sabit **+1 şüphe** (`AddSuspicionRaw`,
yani itibarla yumuşatılmaz — anketin sabit faturası).

**Siyaset**

| Hamle | Skill | Bedel | Getiri |
|---|---|---|---|
| Hareket | c2 | 10.000 | +4 nüfuz × verim, +1.5 itibar |
| Bağış | c5 | 50.000 | +8 nüfuz × verim, +0.5 şüphe |
| Partini fonla | c7 | 50.000 | +8 × **1.6** × (0.5+destek) × verim |
| Hepsini fonla | c8 | 40.000 × parti | +8 × 1.15 × verim, **şüphe yok** |
| Konsensüs | c21 | — | nüfuz kazanımı ×1.25, şüphe kazanımı ×0.9 (kalıcı) |

**Kadro:** burs 0.5/dk, kamp +1.0/dk, eğitim endeksiyle 0.5x–1.5x ölçeklenir.
Yerleşim başına: holding 15/sn para, akademi 0.02/sn nüfuz, medya +0.03 erişim (tavan 0.6).

**Hassas konu çarpanı: 2.5x.** "Olasılığı fazla olmayacak ama getirisi fazla olacak" kuralı
buradan geliyor; hamlenin konusu haritanın hassas konusuna denk gelirse getiri katlanır
(b5/b9/b10 için geçerli).

## 5. Yoruma dayanan üç karar

Notlarda yazmıyordu, ben karar verdim — itiraz edersen tek satır değişir:

1. **"Boş arazi" = `RegionType.Urban`.** Haritada el değmemiş doğa bölgesi bu; `CountryData`
   log'unda da "Doğa" diye etiketli. b4'ün getirisi bu oranla ölçekleniyor
   (`charityMinLandFactor = 0.3` → hiç boş arazi yokken bile kazancın %30'u kalıyor).
2. **b5'in konu tablosu.** Baskın bölge → gündem: Sanayi→StockMarket, Şehir→RealEstate,
   Tarım→Tax, Doğa→General. `TopicType`'ta doğa/çevre konusu olmadığı için sonuncusu zayıf.
3. **b8 "konuyu dağıtma" = gündemi General'e çevirmek.** Oyuncuya konu seçtirecek UI olmadığı
   için o an trendde ne varsa bastırılıp yerine sıradan gündem konuyor.

## 6. Bilinen boşluklar

**Anket sonuçlarını gösterecek ekran yok.** Sonuç `OnPollCompleted` event'i + `Debug.Log` ile
veriliyor, yani konsolu açmayan oyuncu için anketler görünmez. **Küçük bir sonuç kutusu
yazılana kadar b14 (feed yatkınlığı) yarım sayılmalı.**

b11–b13 için durum değişti: `StatHUD` eklendiğinden beri itibar ve şüphe zaten sürekli
ekranda. **Bu, o üç anket skill'ini anlamsızlaştırır** — varlık sebepleri statların gizli
olmasıydı. İki tasarım arasında seçim yapılmalı:
- barlar sürekli açık kalacaksa b11/b12/b13'e başka bir iş verilmeli (ör. hata payını
  daraltmak yerine tahmini kesinleştirmek, ya da rakip/olay bilgisi vermek),
- ya da `StatHUD.hideUntilPollUnlocked` açılmalı: barlar ancak ilgili anket alınınca görünür,
  anketler "ölçüm cihazını satın alma" hâline gelir. Nüfuz barı yok, yani b13 bu durumda da
  boşta kalır.

**Parti seçimi otomatik.** c7 alınınca en yüksek destekli parti otomatik seçiliyor
(`BackStrongestParty`). Seçim ekranı gelince `PoliticsSystem.BackParty(index)` doğrudan
bağlanabilir. Partiler şimdilik isim + destek oranından ibaret; ideolojileri yok.

**Kadro dağıtımı otomatik.** Açık alanlara sırayla dağıtılıyor. `PlaceCadre(track)` public,
UI gelince oyuncuya seçtirilebilir.

**Etiketler hâlâ ters.** B ağacı `Politics` klasöründe ve `SkillBranch.Politics` flag'iyle
duruyor, C ağacı `Media`'da — yani oyunda medya skilleri Siyaset renginde/filtresinde
görünecek. Kod bu karışıklıktan etkilenmiyor (id'lere bakıyor), sadece sunum yanlış.
Ayrıntı: [politics-media-design.md](../Skills/politics-media-design.md) §1.

**Şüphe düşürme hâlâ serbest.** `AddSuspicion`'a negatif değer geçmek engellenmedi.
Kod tabanında bunu yapan tek yer kümes zinciri (`CropDepotSystem:715`, bilinçli). Kural
"şüphe genelde azalmaz" olduğu için ileride bilinçli bir `ReduceSuspicion(sebep)` kapısına
çevrilmesi konuşulmalı.
