using System.Collections.Generic;
using UnityEngine;

// REGION CONVERSION — boş araziyi (biome 1 / Urban) şehre veya sanayiye çevirme. a29
// "kırsaldan kente göç" açıldıysa tarım arazisi (biome 4) de kaynak olabilir.
//
// Repaint() tüm haritayı sıfırdan kurar; dönüşüm ise SADECE seçilen tile'lara dokunmalı,
// yoksa oyuncunun sahip olduğu mülkler, limanlar, gemiler ve yollar da yeniden üretilir.
// Neyse ki yerleşim fonksiyonları zaten bir tile LİSTESİ alıyor (PlaceIndustrialLayout,
// FillLayer) — burada onları alt küme ile çağırıyoruz.
//
// SINIRLAR (bilerek yapılmadı):
//   * Yeni bölgeye YOL çekilmez. RoadGenerator.GenerateRoads tüm haritayı yeniden üretir;
//     bir yamaya yol eklemek ayrı bir iş. Yeni şehir mahallesi yolsuz kalır.
//   * Belediye binası taşınmaz; yeni şehir alanı MEVCUT belediyeye göre katmanlanır.
//     Belediyeden çok uzaktaki dönüşümler en dış katmanın kurallarıyla dolar.
public partial class MapDecorPlacer
{
    private BiomePaintSettings cachedSettings;

    [Header("Bölge Dönüşümü")]
    [Tooltip("Dönüşen alana OTOMATİK bina yerleştirilsin mi. KAPALI: arazi tür değiştirir ama " +
             "boş kalır — oyuncu binaları kendisi kurar/satın alır.")]
    public bool placeBuildingsOnConversion = false;
    [Tooltip("Dönüşen alandaki eski dekor (urban ağaçları vb.) temizlensin mi. " +
             "Şehir/sanayi olan bir arazide orman durması tuhaf görünür.")]
    public bool clearDecorOnConversion = true;

    /// <summary>Dönüşüm için geçerli hedefler. Değerler biome indeksleridir.</summary>
    public enum ConvertTarget
    {
        Cities     = 2,
        Industrial = 3,
    }

    /// <summary>Boş arazi biome indeksi — dönüşümün varsayılan kaynağı.</summary>
    private const int URBAN_BIOME = 1;

    /// <summary>
    /// Tarım arazisi de dönüştürülebilir mi. a29 "kırsaldan kente göç" açar
    /// (UnlockFarmlandConversionEffect). Kapalıyken tarlalar dokunulmazdır.
    /// </summary>
    private bool agriculturalConvertible;

    public bool AgriculturalConvertible => agriculturalConvertible;

    /// <summary>UnlockFarmlandConversionEffect tarafından çağrılır.</summary>
    public void SetAgriculturalConvertible(bool on) => agriculturalConvertible = on;

    public bool CanConvert => cachedMap != null && cachedSettings != null;

    /// <summary>
    /// Tile dönüşüme uygun mu: uygun kaynak biyom, gerçek kara, sisin altında değil.
    /// Şehre çevirirken ayrıca yola çok yakın olmamalı — Repaint'teki bina filtresinin aynısı,
    /// yoksa binalar yolun üstüne oturur.
    ///
    /// Kaynak biyom normalde yalnızca boş arazidir; a29 açıldıysa tarım arazisi de sayılır.
    /// </summary>
    public bool IsConvertible(Vector2Int tile, ConvertTarget target)
    {
        if (cachedMap == null) return false;
        if (tile.x < 0 || tile.x >= cachedMap.width || tile.y < 0 || tile.y >= cachedMap.height) return false;

        if (!cachedMap.IsActionableLand(tile.x, tile.y)) return false;

        int biome = cachedMap.GetBiome(tile.x, tile.y);
        bool validSource = biome == URBAN_BIOME ||
                           (biome == AGRICULTURAL_BIOME && agriculturalConvertible);
        if (!validSource) return false;

        if (cachedMap.GetFog(tile.x, tile.y) > 0.6f) return false;

        if (target == ConvertTarget.Cities &&
            RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated &&
            RoadGenerator.Instance.GetDistanceToRoadEdge(tile.x, tile.y) < cityMinRoadDistance)
            return false;

        return true;
    }

    /// <summary>Merkez + yarıçap içindeki dönüştürülebilir tile'ları toplar (önizleme ve maliyet için).</summary>
    public List<Vector2Int> CollectConvertible(Vector2Int center, int radius, ConvertTarget target)
    {
        var result = new List<Vector2Int>();
        if (cachedMap == null) return result;

        int r2 = radius * radius;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > r2) continue;

            Vector2Int tile = new Vector2Int(center.x + dx, center.y + dy);
            if (IsConvertible(tile, target)) result.Add(tile);
        }

        return result;
    }

    /// <summary>
    /// Tile'ları hedef biyoma çevirir ve üzerlerine uygun yerleşimi kurar.
    /// Dokuyu boyamak çağıranın işi (MapPainter.RepaintBiomeRegion) — burada yalnızca
    /// veri + dekor değişir.
    /// </summary>
    public bool ConvertRegion(List<Vector2Int> tiles, ConvertTarget target, out RectInt affected)
    {
        affected = new RectInt(0, 0, 0, 0);

        if (!CanConvert || tiles == null || tiles.Count == 0) return false;

        //1) biyom verisi — apply:false ile toplu yaz, tek seferde yükle
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector2Int t = tiles[i];
            cachedMap.SetTile(t.x, t.y, true, (int)target, apply: false);

            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
        }

        cachedMap.ApplyTileEdits();
        affected = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

        //2) tarla mozaiği ayrı bir quad olarak haritanın ÜSTÜNDE duruyor; biyomu değiştirmek
        //   onu silmez. Temizlenmezse yeni şehrin altından ekin parselleri görünmeye devam eder.
        ClearCropTiles(tiles);

        //3) dönüşen alanda duran eski dekoru (urban ağaçları vb.) temizle
        if (clearDecorOnConversion) RemoveDecorInTiles(tiles);

        //4) yeni yerleşim — İSTENİRSE. Varsayılan KAPALI: oyuncu arsayı alır, binayı kendi kurar.
        if (placeBuildingsOnConversion) PlaceLayoutForConverted(tiles, target);

        //5) önbellekleri tazele — bina listesi değişti
        InvalidatePropertyIndex();
        InvalidateBuildingImposter();

        //yeni binalar mevcut zoom LOD'unu almalı (Repaint sonundaki -1 hilesinin aynısı)
        shadowLod = -1;

        if (dayNight != null) ApplyCrossfade(dayNight.LightingRatio);

        OnDecorPlaced?.Invoke();
        return true;
    }

    /// <summary>
    /// Dönüşen alana mevcut yerleşim makinesiyle bina kurar. Yalnızca
    /// placeBuildingsOnConversion açıkken çağrılır.
    /// </summary>
    private void PlaceLayoutForConverted(List<Vector2Int> tiles, ConvertTarget target)
    {
        float halfW = cachedHalfW;
        float halfH = cachedHalfH;

        if (target == ConvertTarget.Industrial)
        {
            PlaceIndustrialLayout(cachedMap, cachedSettings, tiles, halfW, halfH);
        }
        else
        {
            //şehir: tile'ları MEVCUT belediyeye göre katmanlara ayır, sonra katmanları doldur
            Vector2Int hall = cityHallTileCached;

            if (hall.x < 0)
            {
                //hiç belediye yoksa yeni alanın kendi merkezini belediye kabul et
                hall = FindCityHallTile(tiles);
                if (hall.x >= 0)
                {
                    TryPlaceCityHall(cachedMap, cachedSettings, hall, halfW, halfH);
                    cityHallTileCached = hall;
                }
            }

            if (hall.x >= 0)
            {
                //kenar seyrelmesi yeni alanı da görsün
                for (int i = 0; i < tiles.Count; i++)
                {
                    float d = Vector2Int.Distance(tiles[i], hall);
                    if (d > cityRadiusTiles) cityRadiusTiles = d;
                }
                BuildCityEdgeDistance(cachedMap);

                List<List<Vector2Int>> layerPools = ClassifyLayers(tiles, hall, cachedSettings.cityLayers);
                for (int i = 0; i < cachedSettings.cityLayers.Count; i++)
                    FillLayer(cachedMap, cachedSettings, cachedSettings.cityLayers[i], hall, layerPools[i], halfW, halfH);
            }
        }
    }

    /// <summary>
    /// Dönüşen tile'lardaki eski dekoru siler. Urban arazi ağaçlarla doludur; şehir/sanayi
    /// binaları onların üstüne oturursa alan çöp gibi görünür.
    /// Oyuncunun SAHİP OLDUĞU binalara dokunulmaz.
    /// </summary>
    private void RemoveDecorInTiles(List<Vector2Int> tiles)
    {
        var set = new HashSet<Vector2Int>(tiles);
        int removed = 0;

        for (int i = cityBuildings.Count - 1; i >= 0; i--)
        {
            BuildingData bd = cityBuildings[i];
            Vector2Int tile = new Vector2Int(bd.tileX, bd.tileY);

            if (!set.Contains(tile)) continue;
            if (IsPropertyProtected(tile)) continue; //sahipli mülk yıkılmaz

            decorObjects.Remove(bd.go);
            if (bd.go != null) Destroy(bd.go);
            cityBuildings.RemoveAt(i);
            removed++;
        }

        if (removed > 0) Debug.Log($"MapDecorPlacer: dönüşüm alanında {removed} eski dekor kaldırıldı.");
    }
}
