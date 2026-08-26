# Tarım Dalı (CropDepotSystem) — YARIM SİSTEM

> **Bu dosya bir uyarıdır.** Depolar kuruluyor, haritada görünüyor, ürün biriktiriyor
> ve tavuk zinciri otomatik alım yapıyor — ama **oyuncu stoğunu elle satamıyor**:
> sistemin hiçbir ekranı yok. `SellDepot` / `SellAll` yazıldı, çağıran yok.

Finance/Farming dalının tamamı (`a26`–`a32`) bu dosyada anlatılan sistemin üstünde
çalışır. Dalın tasarım fikri: **aynı stoğun üstünde yarışan iki kol.**

```
a26  ekin deposu                     ← stok üretir, herkesin bağlı olduğu kök
├─ a27  tavuk zinciri                GÜVENLİ kol: garantili alım + şüphe düşer
│  └─ a29  kırsaldan kente göç       tarlayı şehre çevirir
│     └─ a30  vergi regülasyonu      kalıcı %15 kazanç, +8 şüphe
└─ a28  stokçuluk + talep dalgası    AÇGÖZLÜ kol: kapasite ↑, zirvede sat
   └─ a31  zehirleme                 üretim ×1,75, sürekli şüphe
      └─ a32  yumaklı file           satış ×1,35, itibar yakar  ⚠ YORUM
```

## Orijinal tasarım notları (ARŞİV)

Asset'lerdeki `description` alanları artık skill'in NE YAPTIĞINI anlatıyor (tooltip'te
gösterilen metin bu). Aşağıdakiler, uygulamadan önce o alanlarda duran **özgün tasarım
notlarıdır** — dalın tek yazılı spec'iydi, üzerine yazıldığı için burada saklanıyor.

| Skill | Orijinal not (verbatim) |
|---|---|
| `a26` | tarım root ekin deposu kurma |
| `a27` | los pollos hermanos |
| `a28` | tüketiciye sokma- stokçuluk, sosyal medyada akım yaratıp insanlara alışveriş yaptırma, feede düşer |
| `a29` | kırsaldan kente göç |
| `a30` | vergi regülasyonu |
| `a31` | zehirleme |
| `a32` | yumaklı files |

## Çekirdek döngü (a26)

1. `a26` açılır → `UnlockCropDepotEffect` sistemi yetkilendirir.
2. Oyuncu `a26` node'una tıklar → `EnterCropDepotPlacementEffect` yerleştirme modunu
   açar (aktif yetenek, bekleme yok).
3. Haritada **tarım bölgesindeki** (biyom 4) bir kareye tıklanır; `depotCost` ödenir,
   depo kurulur. Sağ tık / Esc iptal eder.
4. Depo, `collectionRadiusTiles` yarıçapı içindeki **ekili** tile sayısıyla orantılı
   hızda üretir, kapasitesi dolunca durur.
5. Oyuncu stoğu güncel fiyattan satar — **bu adım için ekran yok.**

Yer seçimi gerçek bir karardır: hem verim hem kapasite çevredeki ekili tile sayısından
türer (`MapDecorPlacer.CountCropTilesAround`), yani tarlanın ortası kenarından iyidir.
Boş parseller ve yol koridorları ekili sayılmaz.

Fiyat, temel fiyata geri çekilen rastgele yürüyüşle dalgalanır. Saf rastgele yürüyüş
bir süre sonra tavana/tabana yapışıyordu; geri çekme kuvveti fiyatı dalgalı ama
ortalaması sabit tutuyor.

## İki kol nasıl yarışır

**a27 — tavuk zinciri (los pollos hermanos).** Her `chainTickSeconds` saniyede stoğun
`chainPurchaseRatio` kadarını alır ve **temel** fiyatın `chainPriceRatio` katını öder.
Meşru bir vitrin olduğu için her alımda şüphe düşer.

Zincirin fiyatı kasten güncel piyasadan değil TEMEL fiyattan hesaplanır ve **talep
dalgasından etkilenmez**. Aksi halde a28'in zirvesi otomatik olarak zincire de yansır,
"zirvede elle satmak" anlamsızlaşır ve iki kol arasındaki gerilim kaybolurdu.

**a28 — stokçuluk + talep dalgası.** Pasif kısım kapasiteyi
`hoardingCapacityMultiplier` katına çıkarır. Aktif kısım
(`TriggerDemandWaveEffect`, 120 sn bekleme) sosyal medyada akım başlatır ve fiyatı
geçici olarak yukarı çeker; **çarpan elde tutulan stokla orantılıdır**
(`demandSpikeFullStockUnits`'e kadar doğrusal). Bedeli şüphe.

Gerilim şurada: zincir stoğu sürekli boşalttığı için, iki kolu birden alan oyuncunun
dalga anında elde tuttuğu stok azalır. İkisini birden almak serbesttir ama birbirini yer.

## Şu an ne YOK (yapılacak)

1. **Ekran.** En büyük eksik. Gereken: stok/kapasite çubuğu, güncel birim fiyat ve
   temel fiyata oranı (`PriceRatio`), dalga sayacı, "Sat" düğmesi. Tüm veri hazır:
   `GetInfo(i)`, `GetTotalStock()`, `GetTotalStockValue()`, `PricePerUnit`,
   `DemandSpikeRemaining`, `PoultryChainTotalRevenue` ve `OnPriceChanged` /
   `OnDepotPlaced` / `OnSold` / `OnPoultryChainTick` / `OnDemandSpikeStarted`
   event'leri. Ev sahibi örneği: `PetroleumSystem` → `PetroleumSkillUI` ayrımı.
2. **Feed'de gönderi yok.** `PostDatabase.asset` **boş** (`allPosts: []`). a28'in
   dalgası `SetPlayerOverride` çağırıyor ve fiyat gerçekten yükseliyor, ama feed'de
   gösterilecek gönderi olmadığı için "feede düşer" kısmı görsel olarak boş kalıyor.
   Konu `demandSpikeTopic` ile ayarlanır; şu an `General`. Yeni bir `TopicType`
   eklenecekse **yalnızca enum'un SONUNA** eklenmeli — asset'ler indeks saklıyor.
3. **Yerleştirme önizlemesi.** `PreviewSite(tile, ...)` yazıldı, çağıran yok — oyuncu
   tıklamadan önce o noktanın kaç ekili tile topladığını göremiyor.
   `RegionConversionSystem`'in maske overlay'i örnek alınabilir.
4. **Reddedilme geri bildirimi.** Geçersiz kareye tıklandığında `OnPlacementRejected`
   sebep metnini yayıyor (tarım bölgesi değil / başka depoya çok yakın / yeterli tarla
   yok / depo sınırı doldu), dinleyen yok.
5. **Depo sprite'ı.** `depotSprite` boşsa kod pixel-art bir yer tutucu (ambar + silo)
   üretir. Gerçek sprite atanmalı; **taban sprite'ın alt kenarıdır** (kod pivot'tan
   bağımsız olarak alt kenarı zemine oturtur).
6. **Gece/gündüz.** Depo `CreateCityBuildingObject` yolunu kullanmıyor; gece kararmıyor
   ve gölge almıyor. Tek statik sprite.
7. **Kayıt/yükleme.** Depolar kaydedilmiyor — ama projede genel bir kayıt sistemi de yok.

## Sahne bağlantısı — TEK ELLE ADIM

`CropDepotSystem` **yeni bir MonoBehaviour'dır ve hiçbir sahnede/prefab'da yoktur.**
Eklenene kadar `a26`–`a28`, `a31`, `a32` satın alınabilir ama hiçbir şey yapmaz
(efektler konsola uyarı yazar). `a29` ve `a30` bu bileşen olmadan da çalışır.

1. `enesyeni.unity` sahnesini aç.
2. `RealEstateSystem` ve `RegionConversionSystem` bileşenlerinin durduğu **Managers**
   nesnesini seç (prefab örneği, fileID `1037351544`).
3. **Add Component → Crop Depot System.**
4. Hepsi bu. `mapCamera` boş bırakılabilir (`Camera.main` kullanılır), `depotSprite`
   boş bırakılırsa pixel-art yer tutucu üretilir.

Notlar:

- Bileşen prefab'a değil, sahnedeki örneğe eklenir (override) — `RealEstateSystem`
  de böyle eklenmişti, `Assets/EnesPrefabs/Managers.prefab` içinde yok.
- `Awake` içindeki kopya koruması bilerek `Destroy(this)` çağırır,
  `Destroy(gameObject)` DEĞİL — aksi halde bir kopya `SkillTreeManager` dahil
  paylaşılan nesnedeki her şeyi götürür.
- Skill ağacı paneli tam ekrandır ve açıkken kamera kilitlidir. Yerleştirme modu
  `UImanager.SetMapToolMode(true)` çağırarak paneli kapatır ve kamerayı serbest
  bırakır (`RegionConversionSystem` ile aynı sözleşme) — bunun için ayrıca bir şey
  yapmanız gerekmez.

## a29 — dönüşümün tarlaya etkisi

`a29` iki şey yapar: `MapDecorPlacer.SetAgriculturalConvertible(true)` ile bölge
dönüşümünün tarımı da kaynak kabul etmesini sağlar, ve `RegionConversionSystem`'de
Cities hedefini açar (a38 alınmamış olabilir).

İki nokta önemli:

- **Bedel kendiliğinden doğar.** Tarlayı şehre çevirmek o tarlayı besleyen deponun
  ekili tile sayısını düşürür; `CropDepotSystem` `OnDecorPlaced`'i dinleyip hızları
  yeniden hesaplar. Ayrı bir ceza eklenmedi.
- **Mozaik elle siliniyor.** Parsel mozaiği harita dokusundan AYRI bir quad olarak
  onun üstünde çizilir; biyomu değiştirip haritayı yeniden boyamak mozaiğe dokunmaz.
  `ConvertRegion` bu yüzden `ClearCropTiles` çağırır ve dönüşen karelerin piksellerini
  saydamlaştırır — yoksa yeni şehrin altından ekin görünürdü.

## a32 — YORUM UYARISI

`a32`'nin asset notu yalnızca **"yumaklı files"** ve ne kastedildiği çözülemedi.
Buradaki okuma skill'in ağaçtaki KONUMUNDAN çıkarıldı: `a28` "tüketiciye sokma" ve
`a31` "zehirleme" kolunun devamı olduğu için tüketiciyi kandıran bir satış hilesi
varsayıldı — file içinde ürünü kabartıp olduğundan dolu göstermek.

Uygulanan mekanik: oyuncunun **elden** yaptığı satışlarda birim fiyat
`packagingSaleMultiplier` katına çıkar, her satış `packagingReputationPerSale` kadar
itibar yakar. Tavuk zincirinin alımlarına işlemez (zincir iş ortağıdır, hileyi fark eder).

**Yanlışsa maliyeti düşüktür:** `UnlockDeceptivePackagingEffect`, `CropDepotSystem`
içindeki iki alan ve `A32-.asset`'teki tek bağlantı değişir.

## Denge sayıları — hepsi uydurma

Hiçbiri oynanışla doğrulanmadı; hepsi Inspector'dan ayarlanabilir.

| Alan | Değer | Gerekçe |
|---|---|---|
| `depotCost` | 15.000 | Emlak sistemindeki orta ölçekli bir binaya yakın |
| `collectionRadiusTiles` | 28 | Tarım bölgesinin bir kısmını kapsar, tamamını değil |
| `minDepotSpacingTiles` | 24 | Aynı tarlaya üst üste depo dizmeyi engeller |
| `maxDepots` | 6 | — |
| `minCropTilesToBuild` | 40 | Çorak araziye depo dikmeyi engeller |
| `yieldPerCropTilePerSecond` | 0,015 | ~500 ekili tile → ~7,5 birim/sn |
| `capacityPerCropTile` | 4 | ~500 ekili tile → 2.000 birim, ~4,5 dakikada dolar |
| `basePricePerUnit` | 14 | 2.000 birim ≈ 28.000 → depo bir dolumda kendini amorti eder |
| `priceMinRatio` / `priceMaxRatio` | 0,65 / 1,55 | Beklemenin anlamlı olacağı kadar geniş |
| `chainPurchaseRatio` | 0,25 | Stoğu boşaltır ama tamamen kurutmaz |
| `chainPriceRatio` | 0,85 | Piyasa ortalamasının altında — garanti bedeli |
| `chainSuspicionPerTick` | 0,4 | Aklama hissi versin, şüpheyi tek başına sıfırlamasın |
| `hoardingCapacityMultiplier` | 2,5 | Dalgayı beklemeye değecek kadar depo |
| `demandSpikeMaxMultiplier` | 2,2 | Dolu depoyla satış ≈ iki katı |
| `demandSpikeFullStockUnits` | 3.000 | Stokçuluklu tek deponun dolusu civarı |
| `demandSpikeSeconds` / bekleme | 45 / 120 | Satmaya yetecek kadar uzun, spam'e yetmeyecek kadar seyrek |
| `demandSpikeSuspicion` | 6 | — |
| `poisonYieldMultiplier` | 1,75 | Riski hak edecek kadar büyük |
| `poisonSuspicionPerTick` | 1,2 / 10 sn | Üretim SÜRERKEN birikir; depolar dolunca durur |
| `poisonReputationHit` | 12 | Tek seferlik |
| `packagingSaleMultiplier` | 1,35 | ⚠ yorum |
| `packagingReputationPerSale` | 1,5 | ⚠ yorum |
| `a30` kazanç çarpanı / şüphe | ×1,15 / +8 | Vergi indirimi lobisi |
