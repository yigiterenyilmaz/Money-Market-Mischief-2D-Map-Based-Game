using System.Collections.Generic;
using UnityEngine;

// CROP DEPOT SUPPORT — ekin deposunun (a26 / CropDepotSystem) haritaya sorduğu sorgular.
//
// Deponun üretim hızı ve kapasitesi çevresindeki EKİLİ tile sayısından türer; o küme
// MapDecorPlacer.AgriculturalFields içindeki private cropFieldTiles'tır. Sorguyu oraya
// eklemek yerine ayrı bir partial'a koyuyoruz: AgriculturalFields mozaik üretiminin
// tamamını taşıyan paylaşılan bir dosya, burası ise yalnızca tarım dalına ait. Partial
// sınıf üyeleri paylaştığı için private alana erişim yine de mümkün.
public partial class MapDecorPlacer
{
    /// <summary>Tarım biyomu indeksi — ekin deposu yalnızca bu biyoma kurulabilir.</summary>
    public const int AGRICULTURAL_BIOME = 4;

    // Parsel mozaiğinin tile→piksel eşlemesi. PlaceAgriculturalFields doldurur; ClearCropTiles
    // dönüşen tarlaların piksellerini silmek için okur. cropFieldPixelsPerTile 0 ise doku yok.
    private int cropFieldOriginX;
    private int cropFieldOriginY;
    private int cropFieldPixelsPerTile;

    /// <summary>Tile parsel mozaiğinde EKİLİ mi (boş parseller ve yol koridorları hariç).</summary>
    public bool IsCropTile(Vector2Int tile)
    {
        if (cachedMap == null) return false;
        if (tile.x < 0 || tile.x >= cachedMap.width || tile.y < 0 || tile.y >= cachedMap.height) return false;

        return cropFieldTiles.Contains(tile.x + tile.y * cachedMap.width);
    }

    /// <summary>Tile tarım bölgesinde mi.</summary>
    public bool IsInAgriculturalRegion(Vector2Int tile) => GetBiomeAt(tile) == AGRICULTURAL_BIOME;

    /// <summary>
    /// Merkezin yarıçapı içindeki (daire) ekili tile sayısı. Deponun geliri buradan gelir:
    /// bereketli tarlanın ortasına kurulan depo, kenarına kurulandan daha çok üretir — yer
    /// seçimi böylece gerçek bir karar olur.
    /// </summary>
    public int CountCropTilesAround(Vector2Int center, int radius)
    {
        if (cachedMap == null || cropFieldTiles.Count == 0 || radius <= 0) return 0;

        int w = cachedMap.width;
        int h = cachedMap.height;
        int r2 = radius * radius;

        //tarama alanını haritaya kırp — kenardaki depolarda döngü harita dışına taşmasın
        int minX = Mathf.Max(0, center.x - radius);
        int maxX = Mathf.Min(w - 1, center.x + radius);
        int minY = Mathf.Max(0, center.y - radius);
        int maxY = Mathf.Min(h - 1, center.y + radius);

        int count = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - center.y;
            int rowBase = y * w;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - center.x;
                if (dx * dx + dy * dy > r2) continue;
                if (cropFieldTiles.Contains(x + rowBase)) count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Verilen tile'ları ekili kümeden çıkarır ve parsel mozaiğinde o karelerin piksellerini
    /// saydamlaştırır. Bölge dönüşümü (a29 — kırsaldan kente göç) çağırır.
    ///
    /// Gerekli, çünkü mozaik harita dokusundan AYRI bir quad olarak onun üstünde çizilir:
    /// biyomu Cities yapmak ve haritayı yeniden boyamak mozaiğe dokunmaz, dolayısıyla yeni
    /// şehrin altından ekin parselleri görünmeye devam eder.
    /// </summary>
    public void ClearCropTiles(List<Vector2Int> tiles)
    {
        if (tiles == null || tiles.Count == 0) return;
        if (cachedMap == null || cropFieldTiles.Count == 0) return;

        int w = cachedMap.width;
        int s = cropFieldPixelsPerTile;

        bool canPaint = cropFieldTexture != null && s > 0;
        Color[] clearBlock = canPaint ? new Color[s * s] : null; //varsayılan = tamamen saydam

        int removed = 0;
        bool textureDirty = false;

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector2Int t = tiles[i];
            if (!cropFieldTiles.Remove(t.x + t.y * w)) continue;
            removed++;

            if (!canPaint) continue;

            int px = (t.x - cropFieldOriginX) * s;
            int py = (t.y - cropFieldOriginY) * s;

            //mozaik yalnızca tarım bölgesinin bbox'ını kaplar — dışarı taşan tile'ı atla
            if (px < 0 || py < 0 || px + s > cropFieldTexture.width || py + s > cropFieldTexture.height)
                continue;

            cropFieldTexture.SetPixels(px, py, s, s, clearBlock);
            textureDirty = true;
        }

        if (textureDirty) cropFieldTexture.Apply(false);

        if (removed > 0)
            Debug.Log($"MapDecorPlacer: bölge dönüşümünde {removed} ekili tile tarla mozaiğinden silindi.");
    }
}
