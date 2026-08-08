using System.Collections.Generic;
using UnityEngine;

// PROPERTY QUERY — emlak sistemine (RealEstateSystem) açılan salt-okunur bina arayüzü.
//
// cityBuildings private bir struct listesidir ve deprem sırasında RemoveAt ile KAYAR;
// bu yüzden dışarıya indeks değil TILE anahtarı verilir. İndeks tablosu liste her
// değiştiğinde geçersizleşir (InvalidatePropertyIndex) ve ilk sorguda tembel kurulur.
//
// Görsel durum (seçim tonu, kırık/onarılmış) burada tutulur çünkü renderer alanları bu
// sınıfın içinde. Crossfade döngüsü (Visuals.cs) SADECE alpha yazar — mevcut rengi okuyup
// yalnızca .a'sını değiştirir — bu yüzden RGB tonu kalıcıdır. Kırık bina tint'i de aynı
// mekanizmaya güvenir.
public partial class MapDecorPlacer
{
    // Hiçbir partial'da Awake yok; burada tanımlamak güvenli.
    // OnDestroy'da temizlemeye gerek yok: yok edilmiş MonoBehaviour'lar Unity'nin sahte-null
    // karşılaştırmasıyla zaten null görünür.
    public static MapDecorPlacer Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    /// <summary>Şehir bölgesi biome indeksi (1=Urban, 2=Cities, 3=Industrial, 4=Agricultural).</summary>
    private const int CITIES_BIOME = 2;

    /// <summary>Bir binanın emlak sistemine açılan salt-okunur görünümü.</summary>
    public struct PropertyView
    {
        public Vector2Int tile;
        public GameObject go;
        public Vector3    worldPosition;
        public bool       isSpecial;    //belediye vb. — satılık değil
        public bool       isTree;       //urban ağaç — bina değil
        public bool       isBroken;     //deprem hasarı
        public float      renderedArea; //dünya birimi kare — fiyatın boyut bileşeni
    }

    private Dictionary<Vector2Int, int> propertyIndex;
    private bool  propertyIndexDirty = true;
    private int   propertyIndexCount = -1; //indeks kurulduğundaki liste uzunluğu — Repaint'i de yakalar
    private float cachedAreaMin      = -1f;
    private float cachedAreaMax      = -1f;

    /// <summary>
    /// Emlak sistemi tarafından sahiplenilen tile'lar. Deprem tam yıkımı bunları ATLAR —
    /// yerine kırık duruma düşürür ki oyuncu onarabilsin (bkz. Earthquake.cs).
    /// </summary>
    private readonly HashSet<Vector2Int> protectedPropertyTiles = new HashSet<Vector2Int>();

    /// <summary>
    /// Kırılmadan ÖNCEKİ sprite'lar. Onarım bunlardan geri yazar.
    /// BuildingData.spriteIndex hangi katmanın listesine ait olduğunu taşımadığı için
    /// (özel binalarda -1) indeksten çözmek yerine referans saklanır. Bina kırılırken
    /// doldurulur — her iki kırma yolu da (tekil + toplu) buradan geçer.
    /// </summary>
    private readonly Dictionary<Vector2Int, (Sprite day, Sprite night)> originalSprites
        = new Dictionary<Vector2Int, (Sprite, Sprite)>();

    /// <summary>Sahip olunan mülklerin tabanındaki nabız halkaları.</summary>
    private readonly Dictionary<Vector2Int, GameObject> propertyMarkers = new Dictionary<Vector2Int, GameObject>();
    private static Sprite markerRingSprite;

    //Halka düşük çözünürlükte + Point filtreyle üretilir ki büyütüldüğünde iri pikselli kalsın
    //ve haritanın piksel-art diline uysun. UISpriteFactory'nin halkası Bilinear + anti-aliased —
    //skill ağacı UI'ında doğru, harita üstünde bulanık duruyor. Bu yüzden ayrı üretiliyor.
    private const int MARKER_RES       = 32;
    private const int MARKER_THICKNESS = 3;

    /// <summary>Sert kenarlı, anti-aliasing'siz halka — piksel-art görünüm için.</summary>
    private static Sprite CreatePixelRing(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, //büyütünce bulanıklaşmasın — iri piksel kalsın
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave,
        };

        float outer  = size * 0.5f - 0.5f;
        float inner  = outer - thickness;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            bool on = d <= outer && d >= inner; //eşik: ara değer YOK
            tex.SetPixel(x, y, on ? Color.white : Color.clear);
        }

        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size),
                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>Kırma işleminden hemen önce çağrılır; ilk kayıt korunur (iki kez kırılırsa ezilmesin).</summary>
    private void CacheOriginalSprites(Vector2Int tile, in BuildingData bd)
    {
        if (originalSprites.ContainsKey(tile)) return;

        originalSprites[tile] = (
            bd.dayRenderer   != null ? bd.dayRenderer.sprite   : null,
            bd.nightRenderer != null ? bd.nightRenderer.sprite : null);
    }

    /// <summary>cityBuildings değiştiğinde çağrılır — indeks ve boyut aralığı tazelenir.</summary>
    public void InvalidatePropertyIndex()
    {
        propertyIndexDirty = true;
        cachedAreaMin      = -1f;
        cachedAreaMax      = -1f;
    }

    private void EnsurePropertyIndex()
    {
        //sayaç kontrolü Repaint'i (toplu ekleme) ve kaçırılan bir invalidasyonu da yakalar
        bool fresh = !propertyIndexDirty
                     && propertyIndex != null
                     && propertyIndexCount == cityBuildings.Count;
        if (fresh) return;

        if (propertyIndex == null) propertyIndex = new Dictionary<Vector2Int, int>(cityBuildings.Count);
        else                       propertyIndex.Clear();

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            Vector2Int key = new Vector2Int(bd.tileX, bd.tileY);
            //aynı tile'da birden fazla kayıt olabilir; tıklamada tek hedef yeterli — ilki kazanır
            if (!propertyIndex.ContainsKey(key)) propertyIndex[key] = i;
        }

        propertyIndexDirty = false;
        propertyIndexCount = cityBuildings.Count;
    }

    /// <summary>Binanın ekranda kapladığı alan (dünya birimi kare) — fiyatın boyut bileşeni.</summary>
    private static float ComputeRenderedArea(in BuildingData bd)
    {
        if (bd.dayRenderer == null || bd.dayRenderer.sprite == null) return 0f;

        Vector2 size  = bd.dayRenderer.sprite.bounds.size; //PPU uygulanmış dünya birimi
        Vector3 scale = bd.go != null ? bd.go.transform.localScale : Vector3.one;

        return size.x * Mathf.Abs(scale.x) * size.y * Mathf.Abs(scale.y);
    }

    /// <summary>Verilen tile'daki binayı döner. Bina yoksa false.</summary>
    public bool TryGetProperty(Vector2Int tile, out PropertyView view)
    {
        view = default;
        EnsurePropertyIndex();

        if (!propertyIndex.TryGetValue(tile, out int i)) return false;

        //liste indeks tablosu kurulduktan sonra kaymışsa bir kez daha dene
        if (i < 0 || i >= cityBuildings.Count)
        {
            InvalidatePropertyIndex();
            EnsurePropertyIndex();
            if (!propertyIndex.TryGetValue(tile, out i)) return false;
            if (i < 0 || i >= cityBuildings.Count) return false;
        }

        BuildingData bd = cityBuildings[i];
        view = new PropertyView
        {
            tile          = tile,
            go            = bd.go,
            worldPosition = bd.go != null ? bd.go.transform.position : Vector3.zero,
            isSpecial     = bd.isSpecial,
            isTree        = bd.isTree,
            isBroken      = bd.isBroken,
            renderedArea  = ComputeRenderedArea(bd),
        };
        return true;
    }

    /// <summary>Tile şehir (Cities) bölgesinde mi.</summary>
    public bool IsInCitiesRegion(Vector2Int tile)
    {
        if (cachedMap == null) return false;
        if (tile.x < 0 || tile.x >= cachedMap.width || tile.y < 0 || tile.y >= cachedMap.height) return false;

        return cachedMap.GetBiome(tile.x, tile.y) == CITIES_BIOME;
    }

    /// <summary>Satın alınabilir bina mı: şehir bölgesinde, özel değil, ağaç değil.</summary>
    public bool IsPurchasable(in PropertyView view)
    {
        return view.go != null && !view.isSpecial && !view.isTree && IsInCitiesRegion(view.tile);
    }

    /// <summary>Belediye binasının tile'ı. Yoksa (-1,-1).</summary>
    public Vector2Int CityHallTile => cityHallTileCached;

    /// <summary>Harita ölçeği — tile başına piksel. Dünya boyutu hesaplayanlar için.</summary>
    public float PixelsPerUnit => pixelsPerUnit;

    /// <summary>Harita boyutu. Harita henüz yoksa false.</summary>
    public bool TryGetMapSize(out int width, out int height)
    {
        width = height = 0;
        if (cachedMap == null) return false;

        width  = cachedMap.width;
        height = cachedMap.height;
        return true;
    }

    /// <summary>Tile'ın biyom indeksi. Harita dışı / su = 0.</summary>
    public int GetBiomeAt(Vector2Int tile)
    {
        if (cachedMap == null) return 0;
        if (tile.x < 0 || tile.x >= cachedMap.width || tile.y < 0 || tile.y >= cachedMap.height) return 0;

        return cachedMap.GetBiome(tile.x, tile.y);
    }

    /// <summary>Tile'ın dünya konumu (ScreenToTile'ın tersi).</summary>
    public Vector3 TileToWorldCenter(Vector2Int tile)
    {
        return new Vector3(
            transform.position.x + (tile.x / pixelsPerUnit) - cachedHalfW,
            transform.position.y + (tile.y / pixelsPerUnit) - cachedHalfH,
            0f);
    }

    /// <summary>Harita hazır mı (Repaint tamamlandı mı).</summary>
    public bool HasMap => cachedMap != null;

    /// <summary>
    /// Binalar tek tek görünür mü. Tier 2'de hepsi gizlenip tek bir imposter quad'a iner —
    /// o zoom'da tıklanacak hedef kalmaz (ve bir piksel onlarca tile'a denk gelir).
    /// </summary>
    public bool BuildingsVisible => shadowLod < 2;

    /// <summary>
    /// Ekran noktasını tile'a çevirir. Harita dışıysa (-1,-1) döner
    /// (PetroleumSystem.ScreenToTile ile aynı sözleşme).
    /// </summary>
    public Vector2Int ScreenToTile(Camera cam, Vector2 screenPoint)
    {
        if (cam == null || cachedMap == null) return new Vector2Int(-1, -1);

        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, cam.nearClipPlane + 1f));

        float localX = wp.x - transform.position.x + cachedHalfW;
        float localY = wp.y - transform.position.y + cachedHalfH;

        int tx = Mathf.FloorToInt(localX * pixelsPerUnit);
        int ty = Mathf.FloorToInt(localY * pixelsPerUnit);

        if (tx < 0 || tx >= cachedMap.width || ty < 0 || ty >= cachedMap.height)
            return new Vector2Int(-1, -1);

        return new Vector2Int(tx, ty);
    }

    /// <summary>
    /// Satın alınabilir binaların en küçük/en büyük kapladığı alan. Fiyatı 0..1 aralığına
    /// normalize etmek için kullanılır. Hiç bina yoksa false.
    /// </summary>
    public bool TryGetPurchasableAreaRange(out float min, out float max)
    {
        if (cachedAreaMin >= 0f && cachedAreaMax > cachedAreaMin)
        {
            min = cachedAreaMin;
            max = cachedAreaMax;
            return true;
        }

        min = float.MaxValue;
        max = float.MinValue;
        bool any = false;

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.isSpecial || bd.isTree || bd.go == null) continue;
            if (!IsInCitiesRegion(new Vector2Int(bd.tileX, bd.tileY))) continue;

            float area = ComputeRenderedArea(bd);
            if (area <= 0f) continue;

            if (area < min) min = area;
            if (area > max) max = area;
            any = true;
        }

        if (!any || max <= min)
        {
            //tek bina ya da hepsi aynı boy — normalize edilecek bir aralık yok
            min = max = 0f;
            return false;
        }

        cachedAreaMin = min;
        cachedAreaMax = max;
        return true;
    }

    // -------------------------------------------------------------------------
    // SAHİPLİK KORUMASI — deprem tam yıkımı sahipli binaları atlar
    // -------------------------------------------------------------------------

    public void SetPropertyProtected(Vector2Int tile, bool prot)
    {
        if (prot) protectedPropertyTiles.Add(tile);
        else      protectedPropertyTiles.Remove(tile);
    }

    public bool IsPropertyProtected(Vector2Int tile) => protectedPropertyTiles.Contains(tile);

    // -------------------------------------------------------------------------
    // GÖRSEL DURUM
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tek bir binayı kırık duruma düşürür (MarkBuildingsBroken'ın tek-tile hâli).
    /// Sahipli binalar yok edilmek yerine buraya düşer ki onarılabilsinler.
    /// </summary>
    public bool MarkPropertyBroken(Vector2Int tile)
    {
        EnsurePropertyIndex();
        if (!propertyIndex.TryGetValue(tile, out int i)) return false;
        if (i < 0 || i >= cityBuildings.Count) return false;

        BuildingData bd = cityBuildings[i];
        if (bd.isBroken || bd.dayRenderer == null) return false;

        CacheOriginalSprites(tile, bd);

        int brokenIdx = -1;
        if (brokenBuildingSprites != null && brokenBuildingSprites.Count > 0)
        {
            brokenIdx = Random.Range(0, brokenBuildingSprites.Count);
            bd.dayRenderer.sprite = brokenBuildingSprites[brokenIdx];

            if (bd.nightRenderer != null)
            {
                bool hasNightBroken = brokenBuildingSpritesNight != null
                                      && brokenIdx < brokenBuildingSpritesNight.Count
                                      && brokenBuildingSpritesNight[brokenIdx] != null;

                bd.nightRenderer.sprite = hasNightBroken
                    ? brokenBuildingSpritesNight[brokenIdx]
                    : brokenBuildingSprites[brokenIdx];
            }
        }

        ApplyTint(bd, brokenBuildingTint);

        bd.isBroken       = true;
        bd.brokenIndex    = brokenIdx;
        cityBuildings[i]  = bd;

        InvalidateBuildingImposter();
        return true;
    }

    /// <summary>
    /// Kırık binayı onarır: kırılmadan önce saklanan sprite'ları geri yazar ve hasar tonunu
    /// temizler. Orijinal kaydı yoksa (bina hiç bu sistemden geçmeden kırıldıysa) false döner —
    /// yanlış sprite geri yazmaktansa onarımı reddetmek yeğdir.
    /// </summary>
    public bool RestoreProperty(Vector2Int tile)
    {
        EnsurePropertyIndex();
        if (!propertyIndex.TryGetValue(tile, out int i)) return false;
        if (i < 0 || i >= cityBuildings.Count) return false;

        BuildingData bd = cityBuildings[i];
        if (bd.dayRenderer == null) return false;

        if (!originalSprites.TryGetValue(tile, out var original)) return false;

        if (original.day != null) bd.dayRenderer.sprite = original.day;
        if (original.night != null && bd.nightRenderer != null) bd.nightRenderer.sprite = original.night;
        originalSprites.Remove(tile);

        ApplyTint(bd, Color.white);

        bd.isBroken      = false;
        bd.brokenIndex   = -1;
        cityBuildings[i] = bd;

        InvalidateBuildingImposter();
        return true;
    }

    /// <summary>
    /// Seçim/sahiplik tonu. Crossfade yalnızca alpha yazdığı için RGB kalıcıdır.
    /// Kırık bina tonunu ezmemek için kırıklarda hasar tonuyla çarpılır.
    /// </summary>
    public bool SetPropertyTint(Vector2Int tile, Color tint)
    {
        EnsurePropertyIndex();
        if (!propertyIndex.TryGetValue(tile, out int i)) return false;
        if (i < 0 || i >= cityBuildings.Count) return false;

        BuildingData bd = cityBuildings[i];
        if (bd.dayRenderer == null) return false;

        Color final = bd.isBroken
            ? new Color(tint.r * brokenBuildingTint.r, tint.g * brokenBuildingTint.g, tint.b * brokenBuildingTint.b)
            : tint;

        ApplyTint(bd, final);
        InvalidateBuildingImposter();
        return true;
    }

    /// <summary>Sahiplik halkasının görünüm ayarları.</summary>
    public struct PropertyMarkerStyle
    {
        public Color color;
        public float widthRatio;     //bina genişliğinin katı
        public float squash;         //dikey basıklık (zemin elipsi)
        public float verticalOffset; //taban hizasından ek kaydırma (dünya birimi)
        public float pulseSpeed;
        public float minAlpha;
        public float maxAlpha;
    }

    /// <summary>
    /// Binanın tabanına nabız atan bir halka koyar/kaldırır — sahip olunan mülkler kalabalık
    /// haritada bir bakışta bulunsun diye.
    ///
    /// Halka binanın ÇOCUĞU olur: bina ölçeğini/pozisyonunu miras alır ve bina yok edilirse
    /// birlikte gider.
    ///
    /// Dikey hizalama: sprite'ın ALT KENARI zemin temas çizgisidir (izometrik binada merkez
    /// değil). Halkanın MERKEZİ oraya konursa yarısı binanın altına sarkar — bu yüzden halka
    /// kendi yarı yüksekliği kadar YUKARI alınır, yani alt kenarı taban çizgisine oturur.
    /// </summary>
    public void SetPropertyMarker(Vector2Int tile, bool on, in PropertyMarkerStyle style)
    {
        if (!on)
        {
            if (propertyMarkers.TryGetValue(tile, out GameObject existing))
            {
                if (existing != null) Destroy(existing);
                propertyMarkers.Remove(tile);
            }
            return;
        }

        EnsurePropertyIndex();
        if (!propertyIndex.TryGetValue(tile, out int i)) return;
        if (i < 0 || i >= cityBuildings.Count) return;

        BuildingData bd = cityBuildings[i];
        if (bd.go == null || bd.dayRenderer == null) return;

        if (!propertyMarkers.TryGetValue(tile, out GameObject marker) || marker == null)
        {
            if (markerRingSprite == null) markerRingSprite = CreatePixelRing(MARKER_RES, MARKER_THICKNESS);
            //not: halka sprite'ı statik olarak paylaşılır — her mülk için yeniden üretilmez

            marker = new GameObject("OwnedMarker");
            marker.transform.SetParent(bd.go.transform, false);

            SpriteRenderer ring = marker.AddComponent<SpriteRenderer>();
            ring.sprite = markerRingSprite;
            //ZEMİN dekoru: binanın ve gölgesinin ALTINDA kalmalı, yoksa halka binanın
            //üstüne binmiş gibi görünür. Gölge sortOrder-1 kullanıyor → halka -2.
            ring.sortingOrder = bd.dayRenderer.sortingOrder - 2;

            marker.AddComponent<PropertyMarkerPulse>();
            propertyMarkers[tile] = marker;
        }

        SpriteRenderer markerRenderer = marker.GetComponent<SpriteRenderer>();
        Sprite buildingSprite = bd.dayRenderer.sprite;

        float buildingWidth = buildingSprite != null ? buildingSprite.bounds.size.x : 1f;
        float baseY         = buildingSprite != null ? buildingSprite.bounds.min.y  : 0f;
        float ringWidth     = markerRenderer.sprite != null ? markerRenderer.sprite.bounds.size.x : 1f;

        //PPU'dan bağımsız ölçek: halkanın dünya genişliği binanın oranı kadar olsun
        float k = ringWidth > 0f ? (buildingWidth * style.widthRatio) / ringWidth : 1f;

        marker.transform.localScale = new Vector3(k, k * style.squash, 1f); //basık = zemin elipsi

        //halkanın ölçeklenmiş yüksekliğinin YARISI kadar yukarı al: alt kenarı taban çizgisine
        //otursun, merkezi değil. Aksi halde halka binanın altında ayrı bir daire gibi durur.
        float ringHeight = ringWidth * k * style.squash;
        float y = baseY + ringHeight * 0.5f + style.verticalOffset;

        marker.transform.localPosition = new Vector3(0f, y, -0.001f);

        marker.GetComponent<PropertyMarkerPulse>()
              .Configure(style.color, style.pulseSpeed, style.minAlpha, style.maxAlpha);
    }

    /// <summary>Alpha'ya DOKUNMADAN RGB yazar — crossfade alpha'yı kendi sürer.</summary>
    private static void ApplyTint(in BuildingData bd, Color rgb)
    {
        if (bd.dayRenderer != null)
            bd.dayRenderer.color = new Color(rgb.r, rgb.g, rgb.b, bd.dayRenderer.color.a);

        if (bd.nightRenderer != null)
            bd.nightRenderer.color = new Color(rgb.r, rgb.g, rgb.b, bd.nightRenderer.color.a);
    }
}
