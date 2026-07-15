# Harita ve Ülke Sistemi

## Genel Bakış

Her oyunda rastgele bir ada haritası üretilir. Harita üretildikten sonra bölge oranları ve ülke özellikleri belirlenir. Oyunun geri kalanı (skill ağacı, eventler, feed) bu verilere bakarak çalışır.

---

## Seed Sistemi

Harita üretimi tek bir seed ile deterministiktir (`MapSeed.cs`):

- `MapGenerator.seed` + `useRandomSeed` inspector'dan ayarlanır. `useRandomSeed` açıkken her üretimde yeni seed seçilir ve `seed` alanına **geri yazılır** — üretilmiş haritanın seed'i buradan okunur/kopyalanır.
- Kod tarafı: `mapGenerator.GenerateMap(12345)` belirli seed ile üretir; `MapSeed.CurrentSeed` aktif seed'i verir. Context menu: **Regenerate (Same Seed)**.
- Her üretim aşaması (`island`, `paint`, `roads`, `decor`, `country`, `faults`, `petroleum`, `treasure`) kendi alt-seed'ini `MapSeed.Apply("faz")` ile tek seed'den türetir → event abone sırası değişse bile determinizm bozulmaz.
- Aynı seed **+ aynı inspector ayarları** = aynı harita (yollar, binalar, petrol yatakları, hazineler, fay hatları, ülke özellikleri dahil).
- Runtime rastgelelik (gemiler, trafik, tornado/deprem zamanlaması) seed'e bağlı DEĞİLDİR — üretim bitince `MapSeed.RandomizeRuntime()` global Random'ı serbest bırakır.

---

## Akış Şeması

```
┌─────────────────────────────────────────────────────────────┐
│                     MapGenerator                             │
│          (256x256 procedural ada haritası üretir)           │
│                                                              │
│  Bölgeler: Urban, Cities, Industrial, Agricultural          │
│  Çıktılar: UrbanRatio, CityRatio, IndustrialRatio,          │
│            AgriculturalRatio                                 │
└──────────────────────┬──────────────────────────────────────┘
                       │ OnMapGenerated
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                     CountryData                              │
│          (harita verisini oyun diline çevirir)              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Bölge oranlarını çeker ve RegionType'a dönüştürür:     │
│     biome 1 → Urban (Şehir dışı yerleşim)                   │
│     biome 2 → Cities (Şehir)                                │
│     biome 3 → Industrial (Sanayi)                           │
│     biome 4 → Agricultural (Tarım)                          │
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
