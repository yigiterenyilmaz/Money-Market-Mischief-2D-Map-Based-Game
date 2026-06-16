using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shared placement helpers: shore/region checks, building radius & overlap,
// edge-thinning gradient, and road-footprint collision.
public partial class MapDecorPlacer
{
    // Kenar seyrelmesi için: belediye tile'ı ve şehrin belediyeden en uzak tile mesafesi (tile).
    private Vector2Int cityHallTileCached = new Vector2Int(-1, -1);
    private float      cityRadiusTiles;
    // Her şehir tile'ının biome-2 bölge KENARINA tile cinsinden uzaklığı (BFS ile).
    // Bölge dışı / harita dışı = 0; kenara bitişik şehir tile'ı = 1; içeri doğru artar.
    private int[,]     cityEdgeDist;

    bool HasShoreBuffer(MapGenerator map, int tx, int ty)
    {
        for (int dx = -cityShoreBuffer; dx <= cityShoreBuffer; dx++)
        for (int dy = -cityShoreBuffer; dy <= cityShoreBuffer; dy++)
        {
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > cityShoreBuffer) continue;
            if (!map.IsLand(tx + dx, ty + dy)) return false;
        }
        return true;
    }

    bool IsInsideRegion(MapGenerator map, int tx, int ty, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int nx = tx + dx, ny = ty + dy;
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) return false;
            if (!map.IsLand(nx, ny) || map.GetBiome(nx, ny) != 2) return false;
        }
        return true;
    }

    bool IsOverlapping(float wx, float wy, float overlapR)
    {
        float minDist = overlapR * 2f;
        foreach (var c in occupiedCenters)
            if (Vector2.Distance(new Vector2(wx, wy), c) < minDist) return true;
        return false;
    }

    // Bina yerleşim yarıçapı (dünya birimi). Sprite'ın GENİŞ ve YÜKSEK boyutundan
    // büyüğünü baz alır — yüksek/izometrik binalar (tower'lar) dikeyde de yeterli yer
    // ayırır, böylece küçük binalar tall sprite'ların üstüne/altına oturmaz.
    // layer.overlapRadius bir TABAN olarak uygulanır: binayı daha da seyreltmek için
    // sprite yarıçapından büyük bir değer gir.
    float ComputeBuildingRadius(Sprite sprite, float scale, float overlapRadius)
    {
        float halfW = (sprite.rect.width  / sprite.pixelsPerUnit) * scale * 0.5f;
        float halfH = (sprite.rect.height / sprite.pixelsPerUnit) * scale * 0.5f;
        float spriteRadius = Mathf.Max(halfW, halfH) * 0.9f;
        return Mathf.Max(spriteRadius, overlapRadius);
    }

    /// <summary>
    /// Her şehir tile'ının biome-2 bölge kenarına uzaklığını (tile) BFS ile hesaplar.
    /// Kaynaklar: bölge dışına (veya harita dışına) komşu olan şehir tile'ları (dist=1).
    /// İçeri doğru artar. Bölge dışı tile'lar 0'da kalır. Kenara doğru seyrelmede kullanılır.
    /// </summary>
    void BuildCityEdgeDistance(MapGenerator map)
    {
        int w = map.width, h = map.height;
        cityEdgeDist = new int[w, h];

        var q = new Queue<Vector2Int>();
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        bool IsCity(int x, int y) =>
            x >= 0 && x < w && y >= 0 && y < h && map.IsLand(x, y) && map.GetBiome(x, y) == 2;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!IsCity(x, y)) { cityEdgeDist[x, y] = 0; continue; }

            bool rim = false;
            for (int i = 0; i < 4; i++)
                if (!IsCity(x + dx4[i], y + dy4[i])) { rim = true; break; }

            if (rim) { cityEdgeDist[x, y] = 1; q.Enqueue(new Vector2Int(x, y)); }
            else       cityEdgeDist[x, y] = int.MaxValue;
        }

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            int d = cityEdgeDist[p.x, p.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = p.x + dx4[i], ny = p.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (cityEdgeDist[nx, ny] <= d + 1) continue;
                cityEdgeDist[nx, ny] = d + 1;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    /// <summary>
    /// Seyrelme parametresi t (0 = yoğun, 1 = en seyrek). Yalnızca ŞEHİR BÖLGESİ (biome 2)
    /// KENARINA uzaklığa göre: tile bölge sınırına edgeThinningBorderTiles tile kala seyrelmeye
    /// başlar, sınırda en seyrek. Doğrusal → tüm bant boyunca görünür gradyan. Şehrin iç omurgası
    /// (kenardan uzak tile'lar) tam yoğun kalır. Şehir düzensiz/ince şekilliyse de simetrik çalışır.
    /// (Belediyeye uzaklık bazlı radyal terim kaldırıldı — uzun/ince bölgelerde asimetrik
    ///  aşırı seyrelmeye yol açıyordu; istenen, sınır çevresinde simetrik bir solma.)
    /// </summary>
    float EdgeThinFactor(int tx, int ty)
    {
        if (edgeThinningBorderTiles <= 0 || cityEdgeDist == null) return 0f;
        if (tx < 0 || tx >= cityEdgeDist.GetLength(0) || ty < 0 || ty >= cityEdgeDist.GetLength(1))
            return 0f;

        int d = cityEdgeDist[tx, ty];
        if (d <= 0) return 0f; // bölge dışı

        float inside = Mathf.Clamp01((float)d / edgeThinningBorderTiles); // 0 kenar, 1 derin
        return 1f - inside;                                               // 1 kenar, 0 derin
    }

    // Kenara doğru seyrelme YOĞUNLUK çarpanı (0..1). Yoğun bölgede 1, kenarda (1-edgeThinning).
    // NOT: Tek başına yoğunluk düşürmek overlap'e doyan (Scatter) katmanlarda görünmez —
    // asıl seyrelme RadialSpacingMultiplier (aralık büyütme) ile sağlanır.
    float RadialDensityMultiplier(int tx, int ty)
    {
        if (edgeThinning <= 0f) return 1f;
        return Mathf.Lerp(1f, 1f - edgeThinning, EdgeThinFactor(tx, ty));
    }

    // Kenara doğru bina ARALIĞI çarpanı (>= 1). Yoğun bölgede 1×, kenarda en fazla
    // (1 + edgeThinning*3)× — effRadius ile çarpılır → binalar kenara doğru fiziksel olarak
    // daha seyrek dizilir. Overlap'e doyan katmanlarda bile çalışan asıl mekanizma budur.
    float RadialSpacingMultiplier(int tx, int ty)
    {
        if (edgeThinning <= 0f) return 1f;
        return Mathf.Lerp(1f, 1f + edgeThinning * 3f, EdgeThinFactor(tx, ty));
    }

    bool IsDenseOverlapping(float wx, float wy, float myRadius)
    {
        for (int i = 0; i < denseOccupied.Count; i++)
        {
            var other = denseOccupied[i];
            float minDist = myRadius + other.z; // iki sprite'ın yarıçapı toplamı
            float dx = wx - other.x;
            float dy = wy - other.y;
            float distSq = dx * dx + dy * dy;

            // Yarı-izometrik derinlik: aday bina, mevcut binanın ARKASINDA (yukarıda, +Y)
            // ise o bina tarafından kısmen örtülür. Ne kadar "tam arkada" ise gerekli aralık
            // o kadar azalır (behindClearanceFactor'a doğru). Yana/öne doğru tam aralık korunur.
            if (behindClearanceFactor < 1f && distSq > 0.0000001f && dy > 0f)
            {
                float upFraction = dy / Mathf.Sqrt(distSq);          // 0 = yanda, 1 = tam arkada
                minDist *= Mathf.Lerp(1f, behindClearanceFactor, upFraction);
            }

            if (distSq < minDist * minDist) return true;
        }
        return false;
    }

    /// <summary>
    /// Bina FOOTPRINT'inin (3D zemin) tile-space extentleri. Pivot'a duyarli + simetrik.
    ///
    /// Iso 2:1 projeksiyonda sprite ekranda 2s genis × s yuksek rhombus olarak gozukur,
    /// AMA 3D dunyada gercek footprint s×s'lik bir karedir. Yol kontrolu icin 3D karesi
    /// kullaniliyor → yatay ve dikey yollarda esit clearance.
    ///
    /// - X (yatay): sprite half-width (s) toplam → +-s/2 her yon
    /// - Y (dikey): sprite half-width (s) toplam → sprite alt kenarindan s yukari
    /// - Sprite'in pivot pozisyonu sprite alt kenarini bulmak icin kullanilir (pivot-aware)
    /// </summary>
    void GetSpriteTileExtents(Sprite sprite, float scale,
                              out int xMin, out int xMax, out int yMin, out int yMax)
    {
        if (sprite == null) { xMin = xMax = yMin = yMax = 0; return; }

        Vector2 pivotPx = sprite.pivot;
        float spritePxToWorld = scale / sprite.pixelsPerUnit;

        // Sprite half-width tile cinsinden (iso ground tile side = s)
        int s = Mathf.CeilToInt(sprite.rect.width * 0.5f * spritePxToWorld * pixelsPerUnit);

        // Sprite alt kenari (pivot'a göre, tile cinsinden, tile pozisyonuna relative)
        int spriteBottomY = Mathf.FloorToInt(-pivotPx.y * spritePxToWorld * pixelsPerUnit);

        // 3D kare footprint: s/2 her yatay yon, s yukari sprite alt kenarinden
        int halfS = s / 2;
        xMin = -halfS;
        xMax = halfS;
        yMin = spriteBottomY;
        yMax = spriteBottomY + s;
    }

    /// <summary>
    /// Bina sprite'i (verilen tile pozisyonunda) HERHANGI bir yol pikseline degiyor mu?
    /// Bounding kutuyu extraBuffer kadar her yonden sisirir, kutu icindeki her tile'i
    /// gercekten kontrol eder. Distance field ile hizli reject yapar.
    /// </summary>
    bool SpriteOverlapsRoad(int tx, int ty, Sprite sprite, float scale, int extraBuffer)
    {
        if (RoadGenerator.Instance == null || !RoadGenerator.Instance.IsGenerated) return false;
        if (sprite == null) return false;

        GetSpriteTileExtents(sprite, scale, out int xMin, out int xMax, out int yMin, out int yMax);

        // Hizli reject — L1 distance field ile bounding kutu disinda mi kontrol et
        int dist = RoadGenerator.Instance.GetDistanceToRoadEdge(tx, ty);
        int maxL1 = Mathf.Max(-xMin, xMax) + Mathf.Max(-yMin, yMax) + 2 * extraBuffer;
        if (dist > maxL1) return false;

        // Tam bounding rect taramasi
        int x0 = tx + xMin - extraBuffer;
        int x1 = tx + xMax + extraBuffer;
        int y0 = ty + yMin - extraBuffer;
        int y1 = ty + yMax + extraBuffer;

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        {
            if (RoadGenerator.Instance.GetDistanceToRoadEdge(x, y) == 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Verilen pozisyonda sprite yola degmiyorsa true doner. Degiyorsa cevreyi
    /// spiral arar — maxShift tile'a kadar deniyor. Yola degmedigi ilk pozisyonu
    /// outX/outY ile dondurur. Bulunamazsa false (binayi atla).
    /// </summary>
    bool TryFindRoadFreePosition(int origX, int origY, Sprite sprite, float scale,
                                 int maxShift, int extraBuffer, out int outX, out int outY)
    {
        if (!SpriteOverlapsRoad(origX, origY, sprite, scale, extraBuffer))
        {
            outX = origX; outY = origY;
            return true;
        }
        for (int r = 1; r <= maxShift; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // sadece dis halka
                int nx = origX + dx, ny = origY + dy;
                if (!SpriteOverlapsRoad(nx, ny, sprite, scale, extraBuffer))
                {
                    outX = nx; outY = ny;
                    return true;
                }
            }
        }
        outX = origX; outY = origY;
        return false;
    }
}
