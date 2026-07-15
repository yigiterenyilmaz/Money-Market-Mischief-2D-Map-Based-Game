using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using System.Collections.Generic;

// Harita dekor sprite'larını (ağaç/bina/araç/lamba, gündüz+gece) TEK SpriteAtlas'a paketler.
//
// NEDEN: Ağaç varyantları ayrı PNG'lerde durduğu sürece farklı varyantlar asla aynı draw call'da
// çizilemez — binlerce ağaç = binlerce SetPass. Atlasta hepsi tek dokuya girer; SpriteRenderer'lar
// (aynı sorting aralığında) dinamik batch'lenir ve draw call sayısı çöker.
//
// KRİTİK AYARLAR:
//   * enableRotation = false  — gölge sistemi (MapDecorPlacer.Visuals) sprite.textureRect'ten UV
//     hesaplar; döndürülmüş paketleme bu UV'leri geçersiz kılar.
//   * enableTightPacking = false — tight packing'de sprite.textureRect kullanılamaz (full-rect şart).
//
// KULLANIM: Tools > Map > Build Map Decor Sprite Atlas. Atlas dosyası MapSprites klasörüne yazılır;
// bir kez oluşturulduktan sonra Unity paketlemeyi build/play'de otomatik günceller.
public static class BuildMapDecorAtlas
{
    const string AtlasPath = "Assets/UI/MapSprites/MapDecorAtlas.spriteatlas";

    static readonly string[] PackableFolders =
    {
        "Assets/UI/MapSprites/Nature",
        "Assets/UI/MapSprites/Urban",
        "Assets/UI/MapSprites/Cities",
        "Assets/UI/MapSprites/Agricultural",
        "Assets/UI/MapSprites/Industrial",
        "Assets/UI/MapSprites/Traffic",
        "Assets/UI/MapSprites/Miscellaneous",
    };

    [MenuItem("Tools/Map/Build Map Decor Sprite Atlas")]
    public static void Build()
    {
        FixSourceTextureCompression();

        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        bool isNew = atlas == null;
        if (isNew)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, AtlasPath);
        }

        // Paketleme: rotation ve tight packing KAPALI (gölge UV'leri full-rect ister — üstteki nota bak).
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings
        {
            enableRotation     = false,
            enableTightPacking = false,
            padding            = 4,
        });

        var texSettings = atlas.GetTextureSettings();
        texSettings.generateMipMaps = false;
        texSettings.sRGB            = true;
        // Filter modu kaynak sprite'larla eşle (pixel-art Point ise atlas da Point olsun — Bilinear
        // zorlamak sprite'ları bulanıklaştırır).
        texSettings.filterMode      = DetectSourceFilterMode();
        atlas.SetTextureSettings(texSettings);

        var platform = atlas.GetPlatformSettings("DefaultTexturePlatform");
        platform.overridden         = true;
        platform.maxTextureSize     = 4096;
        // KRİTİK — atlas SIKIŞTIRMASIZ kalmalı. Kaynak PNG'ler çoğunlukla NPOT boyutlu olduğu için
        // Unity onları tek başlarına zaten sıkıştırmadan kullanıyordu (kayıpsız görünüm). Atlas
        // sayfası POT olduğundan 'Automatic' onu DXT'ye sıkıştırır → küçük sprite'lar ve yumuşak
        // alpha kenarları gözle görülür bozulur. Uncompressed = eski görüntü kalitesiyle birebir.
        platform.format             = TextureImporterFormat.RGBA32;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        atlas.SetPlatformSettings(platform);

        // Mevcut packable'ları temizleyip klasörleri yeniden ekle (idempotent).
        var existing = atlas.GetPackables();
        if (existing != null && existing.Length > 0) atlas.Remove(existing);

        var folders = new List<Object>();
        foreach (string path in PackableFolders)
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (folder != null) folders.Add(folder);
            else Debug.LogWarning($"BuildMapDecorAtlas: klasör bulunamadı, atlandı — {path}");
        }
        atlas.Add(folders.ToArray());

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

        Debug.Log($"BuildMapDecorAtlas: {( isNew ? "oluşturuldu" : "güncellendi")} — {AtlasPath} " +
                  $"({folders.Count} klasör paketlendi).");
    }

    /// <summary>
    /// Kaynak sprite'ların filter modunu örnekler (ilk bulunan texture'dan) — atlas aynı modu
    /// kullansın diye. Kaynak bulunamazsa Bilinear'a düşer.
    /// </summary>
    static FilterMode DetectSourceFilterMode()
    {
        foreach (string f in PackableFolders)
        {
            if (!AssetDatabase.IsValidFolder(f)) continue;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { f });
            foreach (string guid in guids)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer != null) return importer.filterMode;
            }
        }
        return FilterMode.Bilinear;
    }

    /// <summary>
    /// Atlasa giren KAYNAK texture'ları sıkıştırmasız yapar. Kaynak sıkıştırılmış kalırsa Unity
    /// "using compressed format" uyarısı verir: sprite önce kaynak sıkıştırmasından açılıp sonra
    /// atlas sıkıştırmasıyla TEKRAR sıkıştırılır → çifte kayıp. Nihai bellek/dosya maliyetini artık
    /// atlas belirlediği için kaynağı uncompressed yapmak ek maliyet getirmez.
    /// </summary>
    static void FixSourceTextureCompression()
    {
        int fixedCount = 0;
        var existingFolders = new List<string>();
        foreach (string f in PackableFolders)
            if (AssetDatabase.IsValidFolder(f)) existingFolders.Add(f);
        if (existingFolders.Count == 0) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", existingFolders.ToArray());
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureCompression == TextureImporterCompression.Uncompressed) continue;

            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            fixedCount++;
        }
        if (fixedCount > 0)
            Debug.Log($"BuildMapDecorAtlas: {fixedCount} kaynak texture uncompressed'a çevrildi " +
                      "(atlas çifte sıkıştırma uyarısı fix).");
    }
}
