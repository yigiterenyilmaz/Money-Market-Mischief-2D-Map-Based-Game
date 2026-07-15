using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// BUILDING IMPOSTER — uzak zoom (LOD tier 2) için tek dokuya bake edilmiş BİNALAR.
//
// ForestImposter ile aynı desen, ama ağaç DIŞI cityBuildings için (şehir/urban/industrial/özel
// binalar). Tier 2'de bina GameObject'leri gizlenir, yerine harita boyutunda 2 quad (gündüz+gece)
// gösterilir. Ağaç kodundan BİLİNÇLİ olarak bağımsız tutulmuştur (yardımcılar dahil kopyadır):
// orman imposter'ı üzerinde başka bir çalışma yürüyor — bu dosya ona hiç dokunmadan yaşar.
//
// Ağaçlardan farkları:
//   * Seyreltme YOK — binalar tek tek okunabilir kalmalı; bake tam kümeyi çizer.
//   * Renk TINT'i bake'e taşınır (deprem sonrası brokenBuildingTint'li binalar bake'te de kırık
//     tonda görünür).
//   * Deprem binaları DEĞİŞTİREBİLİR (kırık sprite swap / yıkım) → InvalidateBuildingImposter
//     bake'i çöpe atar; tier 2'deysek hemen yeniden bake edilip gösterilir.
//
// Bake CommandBuffer ile kamerasız yapılır (pipeline'dan bağımsız, renk birebir — gerekçe için
// ForestImposter dosyasının not bloğuna bak). Display, Custom/MapForestImposter premultiplied
// shader'ı ve V-ters quad UV'siyle aynı konvansiyonu izler.
public partial class MapDecorPlacer
{
    RenderTexture bImpDayRT;
    RenderTexture bImpNightRT;
    GameObject    bImpRoot;
    MeshRenderer  bImpDayMR;
    MeshRenderer  bImpNightMR;
    Material      bImpDayMat;
    Material      bImpNightMat;
    Material      bImpBakeMat;
    bool          bImpBaked;

    /// <summary>Bake'i garantiler (tembel). Başarılıysa true — bina imposter'ı gösterilebilir.</summary>
    bool EnsureBuildingImposterBaked()
    {
        if (bImpBaked && bImpRoot != null) return true;
        if (cachedMap == null) return false;

        bool anyBuilding = false;
        for (int i = 0; i < cityBuildings.Count; i++)
            if (!cityBuildings[i].isTree && cityBuildings[i].go != null) { anyBuilding = true; break; }
        if (!anyBuilding) return false;

        BakeBuildingImposter();
        return bImpBaked;
    }

    void SetBuildingImposterVisible(bool visible)
    {
        if (bImpRoot == null) return;
        if (bImpRoot.activeSelf != visible) bImpRoot.SetActive(visible);
        // Alpha'lar bir sonraki ApplyCrossfade'de güncellenir (ApplyBuildingLod prevRatio'yu sıfırlar).
    }

    /// <summary>
    /// ApplyCrossfade'den çağrılır — quad'lar binalarla aynı gündüz/gece eğrisini izler. Gece
    /// sprite'ı olmayan binalar gece RT'sine gündüz halleriyle bake edildiği için "gece kaybolmaz"
    /// kuralı doku içinde kodludur.
    /// </summary>
    void UpdateBuildingImposterCrossfade(float dayFadeFactor, float nightFactor)
    {
        if (bImpRoot == null || !bImpRoot.activeSelf) return;

        if (bImpDayMat != null)
        {
            Color c = bImpDayMat.color; c.a = dayFadeFactor; bImpDayMat.color = c;
            if (bImpDayMR != null) bImpDayMR.enabled = c.a > 0.004f;
        }
        if (bImpNightMat != null)
        {
            Color c = bImpNightMat.color; c.a = nightFactor; bImpNightMat.color = c;
            if (bImpNightMR != null) bImpNightMR.enabled = c.a > 0.004f;
        }
    }

    void DestroyBuildingImposter()
    {
        if (bImpRoot != null) Destroy(bImpRoot);
        bImpRoot   = null;
        bImpDayMR  = null;
        bImpNightMR = null;
        if (bImpDayMat   != null) { Destroy(bImpDayMat);   bImpDayMat   = null; }
        if (bImpNightMat != null) { Destroy(bImpNightMat); bImpNightMat = null; }
        if (bImpBakeMat  != null) { Destroy(bImpBakeMat);  bImpBakeMat  = null; }
        if (bImpDayRT    != null) { bImpDayRT.Release();   Destroy(bImpDayRT);   bImpDayRT   = null; }
        if (bImpNightRT  != null) { bImpNightRT.Release(); Destroy(bImpNightRT); bImpNightRT = null; }
        bImpBaked = false;
    }

    /// <summary>
    /// Deprem gibi binaları DEĞİŞTİREN olaylardan sonra çağrılır: eski bake artık yalan söylüyor.
    /// Tier 2'deysek hemen yeniden bake edilip gösterilir (binalar gizliyken görüntü boş kalmasın);
    /// değilsek bir sonraki tier 2 girişinde tembel bake olur.
    /// </summary>
    public void InvalidateBuildingImposter()
    {
        bool wasVisible = bImpRoot != null && bImpRoot.activeSelf;
        DestroyBuildingImposter();
        if (wasVisible && shadowLod >= 2)
        {
            ApplyBuildingLod(shadowLod);
            ReapplyCrossfadeNow(); // yeni quad'ların alpha'sı aynı frame'de doğru olsun
        }
    }

    /// <summary>
    /// Bina LOD'u — SetShadowLod'dan çağrılır. Tier 2'de (bake başarılıysa) ağaç olmayan tüm
    /// binalar gizlenip imposter gösterilir; altında binalar aynen geri gelir. Ağaç LOD'undan
    /// bağımsızdır (o ApplyTreeLod'da).
    /// </summary>
    void ApplyBuildingLod(int lod)
    {
        bool imposterOn = lod >= 2 && buildingImposterEnabled && EnsureBuildingImposterBaked();
        SetBuildingImposterVisible(imposterOn);

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.isTree || bd.go == null) continue;

            bool hide = imposterOn;
            // GO SetActive yerine renderer kapatma — tier geçiş hitch'ini küçültür (gerekçe
            // ApplyTreeLod'daki eşlenik blokta). Görünürler ReapplyCrossfadeNow'da açılır.
            if (hide)
            {
                if (bd.dayRenderer   != null) bd.dayRenderer.enabled   = false;
                if (bd.nightRenderer != null) bd.nightRenderer.enabled = false;
            }
            bd.lodHidden = hide;
            cityBuildings[i] = bd;
        }
        // Crossfade'in yeniden uygulanması çağıranın sorumluluğunda: SetShadowLod ve
        // InvalidateBuildingImposter, ApplyBuildingLod'dan sonra ReapplyCrossfadeNow çağırır.
    }

    // -------------------------------------------------------------------------
    // BAKE
    // -------------------------------------------------------------------------

    struct BuildingBakeItem
    {
        public Mesh      mesh;
        public Matrix4x4 matrix;
        public Texture   texture;
        public Color     tint;        // kırık bina tint'i dahil (alpha=1'e zorlanır)
        public Mesh      nightMesh;
        public Matrix4x4 nightMatrix;
        public Texture   nightTexture;
        public Color     nightTint;
        public int       sortOrder;
    }

    void BakeBuildingImposter()
    {
        float mapW = cachedMap.width  / pixelsPerUnit;
        float mapH = cachedMap.height / pixelsPerUnit;
        const float margin = 1.5f;
        float w = mapW + margin * 2f;
        float h = mapH + margin * 2f;
        Vector3 center = transform.position;

        int maxRes = Mathf.Clamp(forestImposterMaxRes, 512, 4096); // ağaçlarla aynı çözünürlük ayarı
        int resX, resY;
        if (w >= h) { resX = maxRes; resY = Mathf.Max(64, Mathf.RoundToInt(maxRes * h / w)); }
        else        { resY = maxRes; resX = Mathf.Max(64, Mathf.RoundToInt(maxRes * w / h)); }

        bImpDayRT   = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32) { name = "BuildingImposterDay" };
        bImpNightRT = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32) { name = "BuildingImposterNight" };

        // -- Bake listesi: ağaç olmayan TÜM binalar, güncel sprite + tint ile -------------------
        // Matrisler renderer transform'undan alınır → gelecekte eğik/döndürülmüş bina gelirse de
        // doğru bake edilir. (Bake, binalar henüz gizlenmeden çağrılır → transformlar aktif.)
        var meshCache = new Dictionary<Sprite, Mesh>();
        var items     = new List<BuildingBakeItem>();
        int maxOrder  = 10;

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.isTree || bd.go == null || bd.dayRenderer == null || bd.dayRenderer.sprite == null) continue;

            Sprite daySp = bd.dayRenderer.sprite;
            Color dayTint = bd.dayRenderer.color; dayTint.a = 1f;

            var item = new BuildingBakeItem
            {
                mesh      = GetBuildingSpriteMesh(daySp, meshCache),
                matrix    = bd.dayRenderer.transform.localToWorldMatrix,
                texture   = daySp.texture,
                tint      = dayTint,
                sortOrder = bd.dayRenderer.sortingOrder,
            };

            if (bd.nightRenderer != null && bd.nightRenderer.sprite != null)
            {
                Sprite nightSp = bd.nightRenderer.sprite;
                Color nightTint = bd.nightRenderer.color; nightTint.a = 1f;
                item.nightMesh    = GetBuildingSpriteMesh(nightSp, meshCache);
                item.nightMatrix  = bd.nightRenderer.transform.localToWorldMatrix;
                item.nightTexture = nightSp.texture;
                item.nightTint    = nightTint;
            }
            else
            {
                item.nightMesh    = item.mesh;
                item.nightMatrix  = item.matrix;
                item.nightTexture = item.texture;
                item.nightTint    = item.tint;
            }

            if (item.sortOrder > maxOrder) maxOrder = item.sortOrder;
            items.Add(item);
        }

        if (items.Count == 0) { DestroyBuildingImposter(); return; }

        // Canlı çizimle aynı üst üste binme için arkadan öne sırala.
        items.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

        if (bImpBakeMat == null) bImpBakeMat = new Material(Shader.Find("Sprites/Default"));

        Matrix4x4 view = Matrix4x4.Translate(new Vector3(-center.x, -center.y, 0f));
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(-w * 0.5f, w * 0.5f, -h * 0.5f, h * 0.5f, -50f, 50f), true);

        var mpb = new MaterialPropertyBlock();
        var cmd = new CommandBuffer { name = "BuildingImposterBake" };

        // Gündüz pası
        cmd.SetRenderTarget(bImpDayRT);
        cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
        cmd.SetViewProjectionMatrices(view, proj);
        for (int i = 0; i < items.Count; i++)
        {
            mpb.SetTexture("_MainTex", items[i].texture);
            mpb.SetColor("_Color", items[i].tint);
            cmd.DrawMesh(items[i].mesh, items[i].matrix, bImpBakeMat, 0, 0, mpb);
        }

        // Gece pası
        cmd.SetRenderTarget(bImpNightRT);
        cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
        cmd.SetViewProjectionMatrices(view, proj);
        for (int i = 0; i < items.Count; i++)
        {
            mpb.SetTexture("_MainTex", items[i].nightTexture);
            mpb.SetColor("_Color", items[i].nightTint);
            cmd.DrawMesh(items[i].nightMesh, items[i].nightMatrix, bImpBakeMat, 0, 0, mpb);
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        foreach (var kvp in meshCache) Destroy(kvp.Value);

        // -- Quad'lar: tüm binaların üstünde. Tier 2'de binalar gizli olduğu için tek etkileşim
        // diğer dekorla — bu ölçekte algılanamaz. Ağaç imposter'ıyla göreli sırası da önemsizdir.
        bImpRoot = new GameObject("BuildingImposter");
        bImpRoot.transform.SetParent(transform, false);
        bImpRoot.transform.position = new Vector3(center.x, center.y, spriteZ);

        bImpDayMR   = CreateBuildingImposterQuad("Day",   bImpDayRT,   w, h, maxOrder + 1, out bImpDayMat);
        bImpNightMR = CreateBuildingImposterQuad("Night", bImpNightRT, w, h, maxOrder + 2, out bImpNightMat);

        bImpRoot.SetActive(false); // SetBuildingImposterVisible açar
        bImpBaked = true;

        Debug.Log($"MapDecorPlacer: building imposter bake — {items.Count} bina → {resX}x{resY} x2 RT " +
                  "(CommandBuffer, kamerasız).");
    }

    /// <summary>Sprite geometrisinden mesh (ForestImposter'daki eşleniğin bilinçli kopyası —
    /// orman dosyası bağımsız evrilebilsin diye paylaşılmıyor).</summary>
    static Mesh GetBuildingSpriteMesh(Sprite sp, Dictionary<Sprite, Mesh> cache)
    {
        if (cache.TryGetValue(sp, out var m)) return m;

        Vector2[] sv = sp.vertices;
        var verts = new Vector3[sv.Length];
        for (int i = 0; i < sv.Length; i++) verts[i] = sv[i];

        ushort[] st = sp.triangles;
        var tris = new int[st.Length];
        for (int i = 0; i < st.Length; i++) tris[i] = st[i];

        m = new Mesh { name = "BImposterSprite_" + sp.name };
        m.vertices  = verts;
        m.uv        = sp.uv;
        m.triangles = tris;
        m.RecalculateBounds();

        cache[sp] = m;
        return m;
    }

    MeshRenderer CreateBuildingImposterQuad(string name, RenderTexture rt, float w, float h,
                                            int sortingOrder, out Material mat)
    {
        var go = new GameObject("BuildingImposter" + name);
        go.transform.SetParent(bImpRoot.transform, false);

        float hw = w * 0.5f, hh = h * 0.5f;
        var mesh = new Mesh { name = "BuildingImposterQuad" };
        mesh.vertices  = new[]
        {
            new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
            new Vector3(hw, hh, 0f),   new Vector3(-hw, hh, 0f),
        };
        // V dikey ters — RT konvansiyonu (gerekçe ForestImposter'daki eşlenik quad'da).
        mesh.uv        = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        Shader impShader = Shader.Find("Custom/MapForestImposter"); // premultiplied display — ortak
        if (impShader == null)
            Debug.LogWarning("MapDecorPlacer: Custom/MapForestImposter shader'ı bulunamadı — " +
                             "building imposter Sprites/Default fallback ile KOYU görünecek.");
        mat = new Material(impShader != null ? impShader : Shader.Find("Sprites/Default"));
        mat.mainTexture = rt;
        mat.color       = new Color(1f, 1f, 1f, 1f);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder   = sortingOrder;
        return mr;
    }
}
