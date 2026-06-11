# Harita ve Ülke Sistemi

## Genel Bakış

Her oyunda rastgele bir ada haritası üretilir. Harita üretildikten sonra bölge oranları ve ülke özellikleri belirlenir. Oyunun geri kalanı (skill ağacı, eventler, feed) bu verilere bakarak çalışır.

---

## Akış Şeması

```
┌─────────────────────────────────────────────────────────────┐
│                     MapGenerator                             │
│          (256x256 procedural ada haritası üretir)           │
│                                                              │
│  Biome'lar: Forest, Desert, Mountains, Plains               │
│  Çıktılar: ForestRatio, DesertRatio, MountainRatio,        │
│            PlainsRatio                                       │
└──────────────────────┬──────────────────────────────────────┘
                       │ OnMapGenerated
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                     CountryData                              │
│          (harita verisini oyun diline çevirir)              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Biome oranlarını çeker ve RegionType'a dönüştürür:     │
│     Forest    → Agricultural (Tarım)                        │
│     Desert    → Barren (Boş Arazi)                          │
│     Mountains → Industrial (Sanayi)                         │
│     Plains    → Urban (Şehir)                               │
│                                                              │
│  2. Ülke özelliklerini random üretir (0-100):              │
│     corruptionIndex       (yozlaşma endeksi)                │
│     educationIndex        (eğitim seviyesi)                 │
│     climateFertility      (iklim verimliliği)               │
│     naturalResourceWealth (doğal kaynak zenginliği)         │
│                                                              │
└──────────────────────┬──────────────────────────────────────┘
                       │ OnCountryDataReady
                       ▼
              Diğer sistemler veriyi okur

```

---

## Bölge Tipleri

| RegionType | Açıklama | MapGenerator karşılığı |
|------------|----------|----------------------|
| Industrial | Sanayi bölgesi | Mountains (biome 3) |
| Urban | Şehir | Plains (biome 4) |
| Agricultural | Tarım arazisi | Forest (biome 1) |
| Barren | Boş arazi / köy | Desert (biome 2) |

---

## Ülke Özellikleri

| Özellik | Aralık | Açıklama |
|---------|--------|----------|
| corruptionIndex | 0-100 | Yozlaşma endeksi. Yüksekse siyasetten rahat ilerlenir |
| educationIndex | 0-100 | Eğitim seviyesi |
| climateFertility | 0-100 | İklim verimliliği. Yüksekse tarım hattı avantajlı |
| naturalResourceWealth | 0-100 | Doğal kaynak zenginliği. Yüksekse petrol/maden hattı avantajlı |

Değerler 3 random sayının ortalaması alınarak üretilir. Bu sayede ortaya yakın değerler (35-65) çok daha sık gelir, uç değerler (0-13 veya 87-100) nadir ama mümkündür.

---

## Kim Neyi Yapar?

| İş | Sorumlu |
|----|---------|
| Ada haritası üretme | `MapGenerator.GenerateMap()` |
| Biome oranlarını hesaplama | `MapGenerator.CalculateBiomeRatios()` |
| Biome → RegionType dönüştürme | `CountryData.PullRegionRatios()` |
| Ülke özelliklerini random üretme | `CountryData.GenerateCountryProperties()` |
| Bölge oranı sorgulama | `CountryData.GetRegionRatio()` |
| Baskın bölge sorgulama | `CountryData.GetDominantRegion()` |
| Ülke özelliği sorgulama | `CountryData.CorruptionIndex` vb. |

---

## Dosyalar

| Dosya | İçerik |
|-------|--------|
| `RandomMap.cs` | MapGenerator sınıfı, procedural ada üretimi |
| `CountryData.cs` | Harita verisini oyun diline çeviren merkezi veri sınıfı |
| `RegionType.cs` | Bölge tipleri enum'ı (Industrial, Urban, Agricultural, Barren) |

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `MapGenerator.OnMapGenerated` | Harita üretimi tamamlandığında |
| `CountryData.OnCountryDataReady` | Bölge oranları ve ülke özellikleri hazır olduğunda |
