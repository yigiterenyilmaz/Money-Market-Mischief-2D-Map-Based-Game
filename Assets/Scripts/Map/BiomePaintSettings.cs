using System.Collections.Generic;
using UnityEngine;

public enum CityLayerStyle { ManhattanGrid, Grid, Scatter }

[System.Serializable]
public struct CityBuildingEntry
{
    public Sprite daySprite;
    [Tooltip("Gece sprite'ı. Boş bırakılabilir.")]
    public Sprite nightSprite;
    [Tooltip("İzometrik sprite ise işaretle. Gölge mekanizması binanın tabanından diyagonal çıkacak şekilde değişir.")]
    public bool isIsometric;
}

[System.Serializable]
public class CityLayer
{
    [Tooltip("Katman adı (inspector'da görünür).")]
    public string name = "Layer";
    [Tooltip("Bu katmanda kullanılacak bina spriteleri. Boş bırakılabilir — sonradan doldurun.")]
    public List<CityBuildingEntry> sprites = new List<CityBuildingEntry>();
    [Tooltip("Belediye binasından minimum tile uzaklığı (iç sınır, dahil).")]
    public float innerRadius;
    [Tooltip("Belediye binasından maksimum tile uzaklığı (dış sınır, hariç).")]
    public float outerRadius = 60f;
    [Tooltip("İşaretlenirse bu katman, önceki katmanlara girmeyen tüm tile'ları toplar (catch-all). outerRadius > 0 ise yine de o yarıçapla sınırlanır (şehir adaya yayılmaz); outerRadius = 0 ise adanın kenarına kadar uzanır.")]
    public bool extendToEdge;
    [Tooltip("Yerleşim stili: Manhattan ızgarası (avenue+street), düzenli ızgara veya seyrek dağılım.")]
    public CityLayerStyle style = CityLayerStyle.Grid;
    [Tooltip("Sprite ölçek aralığı (min, max).")]
    public Vector2 scaleRange = new Vector2(0.5f, 0.7f);
    [Tooltip("Doluluk yoğunluğu. 1.0 = tıka basa dolu, 0.5 = yarısı boş.")]
    [Range(0.1f, 1f)] public float fillDensity = 1f;
    [Tooltip("Minimum bina aralığı (dünya birimi, yarıçap). Tüm stillerde geçerli. Büyük = binalar daha seyrek. ÖNEMLİ: binalar zaten kendi sprite boyutu kadar aralıklı yerleşir; sprite yarıçapından KÜÇÜK değerlerin etkisi olmaz — seyreltmek için sprite yarıçapından büyük bir değer gir (genelde 0.5–2).")]
    public float overlapRadius = 0.12f;
    [Tooltip("Grid ızgara adımı (tile). Bina çapına yakın olmalı; küçük = sık + çakışmalı.")]
    public int gridStep = 18;
    [Tooltip("Scatter denemesi çarpanı. Büyük = daha fazla bina.")]
    public int scatterRate = 2;
    [Tooltip("Avenue (N-S cadde) aralığı, tile cinsinden. Sadece ManhattanGrid stili.")]
    public int avenueSpacing = 40;
    [Tooltip("Street (E-W sokak) aralığı, tile cinsinden. Sadece ManhattanGrid stili.")]
    public int streetSpacing = 32;
    [Tooltip("Sokak şerit genişliği (tile). Bu kadar tile sokak olarak boş bırakılır. Sadece ManhattanGrid.")]
    public int streetWidth = 3;
}

[System.Serializable]
public struct SpecialCityBuilding
{
    [Tooltip("Özel bina sprite'ı (gündüz). Animasyonlu modda ilk frame olarak kullanılır.")]
    public Sprite daySprite;
    [Tooltip("Özel bina sprite'ı (gece). Boş bırakılabilir.")]
    public Sprite nightSprite;
    [Tooltip("Maksimum kaç adet yerleştirilecek. Yer bulunamazsa daha az olabilir, ama bu sayıdan fazla üretilmez.")]
    [Range(1, 50)] public int count;
    [Tooltip("Hangi katmana yerleşsin (0 = ilk katman; cityLayers listesindeki indeks).")]
    public int targetLayer;
    [Tooltip("Özel ince yol çekilsin mi?")]
    public bool connectToRoad;
    [Tooltip("Etrafında bina olmayacak alan (tile cinsinden).")]
    [Range(0, 30)] public int clearingRadius;
    [Tooltip("İzometrik sprite ise işaretle. Gölge mekanizması binanın tabanından diyagonal çıkacak şekilde değişir.")]
    public bool isIsometric;
    [Tooltip("İşaretlenirse bina animasyonlu olur. animationFrames sırayla döner.")]
    public bool isAnimated;
    [Tooltip("Animasyon frame'leri (sprite sheet'ten dilimlenmiş). Sırayla döner.")]
    public Sprite[] animationFrames;
    [Tooltip("Saniyedeki frame sayısı. 12-15 çoğu animasyon için yeterli.")]
    public float frameRate;
}

[CreateAssetMenu(fileName = "BiomePaintSettings", menuName = "Map/BiomePaintSettings")]
public class BiomePaintSettings : ScriptableObject
{
    [Header("Beach — sandy coastal strip")]
    public Color beachDark  = new Color(0.72f, 0.62f, 0.38f); // wet sand
    public Color beachLight = new Color(0.86f, 0.78f, 0.52f); // dry sand

    [Header("Water")]
    public Color waterDeep    = new Color(0.04f, 0.12f, 0.35f);
    public Color waterShallow = new Color(0.12f, 0.35f, 0.62f);

    [Header("Fog")]
    public Color fogColor = new Color(0.70f, 0.74f, 0.80f);

    [Header("Urban — untouched nature, base green of the country")]
    public Color urbanDark  = new Color(0.18f, 0.42f, 0.14f); // deep natural green
    public Color urbanLight = new Color(0.26f, 0.54f, 0.20f); // slightly lighter green

    [Header("Agricultural — livelier, richer green than urban")]
    public Color agriculturalDark  = new Color(0.20f, 0.52f, 0.12f); // vivid crop green
    public Color agriculturalLight = new Color(0.38f, 0.66f, 0.18f); // bright lush green

    [Header("Cities — pale, concrete-like, washed out urban tone")]
    public Color citiesDark  = new Color(0.52f, 0.52f, 0.48f); // pale grey
    public Color citiesLight = new Color(0.66f, 0.65f, 0.60f); // lighter concrete

    [Header("Industrial — cracked earth, dark, barren")]
    public Color industrialDark   = new Color(0.30f, 0.24f, 0.18f); // dark cracked earth
    public Color industrialLight  = new Color(0.46f, 0.38f, 0.28f); // dry dirt
    public Color industrialCrack  = new Color(0.18f, 0.14f, 0.10f); // deep cracks

    [Header("Sea Rocks — gray stone formations in the ocean")]
    public Color seaRockDark  = new Color(0.38f, 0.38f, 0.40f); // dark gray stone
    public Color seaRockLight = new Color(0.56f, 0.55f, 0.54f); // lighter gray
    public Color seaRockCrack = new Color(0.24f, 0.23f, 0.22f); // deep crevice

    [Header("Decorative Sprites — Urban")]
    public List<Sprite> urbanDecor = new List<Sprite>();

    [Header("Decorative Sprites — Agricultural")]
    public List<Sprite> agriculturalDecor = new List<Sprite>();

    [Header("Decorative Sprites — Industrial")]
    public List<Sprite> industrialDecor = new List<Sprite>();

    // =========================================================================
    // ŞEHİR BİNALARI
    // =========================================================================

    [Header("City Hall — Belediye Binası (tek)")]
    public CityBuildingEntry cityHallEntry;

    [Header("City Layers — Katmanlı Şehir Bölgeleri")]
    [Tooltip("Her katman: iç/dış yarıçap + sprite havuzu + yerleşim stili. Sırayla (iç→dış) işlenir. Başlangıçta boş — 'Populate Default City Layers' ile varsayılan katmanları oluşturun.")]
    public List<CityLayer> cityLayers = new List<CityLayer>();

    [Header("City Special Buildings — Sabit Sayılı Özel Binalar")]
    public List<SpecialCityBuilding> specialCityBuildings = new List<SpecialCityBuilding>();

    [ContextMenu("Populate Default City Layers")]
    void PopulateDefaultCityLayers()
    {
        cityLayers.Clear();

        // Towers — iç çekirdek, Manhattan ızgarası (en büyük binalar)
        cityLayers.Add(new CityLayer
        {
            name          = "Towers",
            innerRadius   = 0f,
            outerRadius   = 55f,
            extendToEdge  = false,
            style         = CityLayerStyle.ManhattanGrid,
            scaleRange    = new Vector2(0.6f, 0.8f),
            fillDensity   = 1f,
            overlapRadius = 0.12f,
            gridStep      = 22,
            scatterRate   = 2,
            avenueSpacing = 40,
            streetSpacing = 32,
            streetWidth   = 4,
        });

        // Buildings — orta halka, düzenli ızgara (orta boy binalar)
        cityLayers.Add(new CityLayer
        {
            name          = "Buildings",
            innerRadius   = 55f,
            outerRadius   = 110f,
            extendToEdge  = false,
            style         = CityLayerStyle.Grid,
            scaleRange    = new Vector2(0.42f, 0.58f),
            fillDensity   = 0.95f,
            overlapRadius = 0.14f,
            gridStep      = 22,
            scatterRate   = 2,
            avenueSpacing = 40,
            streetSpacing = 32,
            streetWidth   = 3,
        });

        // Neighbourhoods — dış halka, seyrek dağılım (küçük evler, catch-all)
        cityLayers.Add(new CityLayer
        {
            name          = "Neighbourhoods",
            innerRadius   = 110f,
            outerRadius   = 0f,
            extendToEdge  = true,
            style         = CityLayerStyle.Scatter,
            scaleRange    = new Vector2(0.28f, 0.42f),
            fillDensity   = 1f,
            overlapRadius = 0.16f,
            gridStep      = 18,
            scatterRate   = 3,
            avenueSpacing = 40,
            streetSpacing = 32,
            streetWidth   = 3,
        });

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("BiomePaintSettings: 3 varsayılan şehir katmanı oluşturuldu. Sprite'ları inspector'dan ekleyin.");
    }

    // Unity'nin inspector "+" butonu yeni liste elemanlarını C# constructor yerine
    // sıfırlarla doldurur — bu da scaleRange=0 (görünmez bina) ve fillDensity=0
    // (hiç bina yok) gibi bozuk katmanlara yol açar. Burada açıkça başlatılmamış
    // (<= 0) alanları makul varsayılanlara çekiyoruz. Kullanıcının girdiği pozitif
    // değerlere dokunmuyoruz.
    void OnValidate()
    {
        if (cityLayers == null) return;
        for (int i = 0; i < cityLayers.Count; i++)
        {
            CityLayer layer = cityLayers[i];
            if (layer == null) continue;
            if (layer.scaleRange == Vector2.zero) layer.scaleRange = new Vector2(0.5f, 0.7f);
            if (layer.fillDensity   <= 0f) layer.fillDensity   = 1f;
            if (layer.gridStep      <= 0)  layer.gridStep      = 18;
            if (layer.scatterRate   <= 0)  layer.scatterRate   = 2;
            if (layer.overlapRadius <= 0f) layer.overlapRadius = 0.12f;
            if (layer.avenueSpacing <= 0)  layer.avenueSpacing = 40;
            if (layer.streetSpacing <= 0)  layer.streetSpacing = 32;
            if (layer.streetWidth   <= 0)  layer.streetWidth   = 3;
            // Boş halka (innerRadius=outerRadius=0) hiç tile almaz — yeni eklenen katman
            // varsayılan bir dış yarıçap alsın (catch-all katmanlar hariç).
            if (!layer.extendToEdge && layer.outerRadius <= 0f) layer.outerRadius = 60f;
        }

        // Özel binalar struct olduğu için listeye geri yazmak gerekir. count=0 (inspector
        // "+" sıfırlaması) hiç bina üretmez — en az 1'e çek.
        if (specialCityBuildings != null)
            for (int i = 0; i < specialCityBuildings.Count; i++)
            {
                var sp = specialCityBuildings[i];
                if (sp.count <= 0) { sp.count = 1; specialCityBuildings[i] = sp; }
            }
    }
}
