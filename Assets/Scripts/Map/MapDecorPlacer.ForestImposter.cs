using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// FOREST IMPOSTER — uzak zoom (LOD tier 2) için tek dokuya bake edilmiş orman.
//
// Ağaçlar Repaint'ten sonra tamamen statiktir. En uzak zoomda binlerce ağaç GameObject'inin
// (her biri 2-3 renderer) draw call maliyetini ödemek yerine, tüm ağaçlar BİR KEZ iki
// RenderTexture'a çizilir (gündüz + gece varyantı) ve tier 2'de ağaçların yerine harita
// boyutunda 2 quad gösterilir. Gündüz/gece geçişi quad alpha'larıyla, binalarla birebir aynı
// sabit-güç (sqrt) eğrisiyle yapılır.
//
// BAKE YÖNTEMİ — CommandBuffer ile DOĞRUDAN çizim, kamera YOK:
// Eski yaklaşım (geçici kamera + layer maskesi + Camera.Render) URP render graph'ta hem depth
// zorunluluğuna takılıyordu hem de pipeline'ın 2D ışık/post-process zincirinden geçtiği için
// bake rengi canlı sprite renginden SAPABİLİYORDU. Şimdi sprite mesh'leri Sprites/Default ile
// RT'ye komut tamponu üzerinden ham çizilir → pipeline'dan tamamen bağımsız, renk birebir.
// Sprites/Default premultiplied yazar (rgb*=a, Blend One OneMinusSrcAlpha) → RT premultiplied
// RGB + düz alpha biriktirir; display tarafı Custom/MapForestImposter aynı premultiplied
// sözleşmeyle basar (bkz. o shader'ın not bloğu).
//
// Bake tembeldir: tier 2'ye İLK girişte yapılır. Clear() bake'i geçersiz kılar.
public partial class MapDecorPlacer
{
    RenderTexture impDayRT;
    RenderTexture impNightRT;
    GameObject    impRoot;
    MeshRenderer  impDayMR;
    MeshRenderer  impNightMR;
    Material      impDayMat;
    Material      impNightMat;
    Material      impBakeMat;   // Sprites/Default — bake çizimlerinde ortak, texture MPB ile verilir
    bool          impBaked;

    /// <summary>Bake'i garantiler (tembel). Başarılıysa true — imposter gösterilebilir.</summary>
    bool EnsureForestImposterBaked()
    {
        if (impBaked && impRoot != null) return true;
        if (cachedMap == null) return false;

        bool anyTree = false;
        for (int i = 0; i < cityBuildings.Count; i++)
            if (cityBuildings[i].isTree && cityBuildings[i].go != null) { anyTree = true; break; }
        if (!anyTree) return false;

        BakeForestImposter();
        return impBaked;
    }

    void SetForestImposterVisible(bool visible)
    {
        if (impRoot == null) return;
        if (impRoot.activeSelf != visible) impRoot.SetActive(visible);
        // Alpha'lar bir sonraki ApplyCrossfade'de güncellenir (ApplyTreeLod prevRatio'yu sıfırlar).
    }

    /// <summary>
    /// ApplyCrossfade'den çağrılır — imposter quad'ları binalarla aynı gündüz/gece eğrisini izler.
    /// Gece RT'sinde gece sprite'ı olmayan ağaçlar gündüz halleriyle bake edildiği için "ışıksız
    /// ağaç gece kaybolmaz" kuralı doku içinde zaten kodludur.
    /// </summary>
    void UpdateForestImposterCrossfade(float dayFadeFactor, float nightFactor)
    {
        if (impRoot == null || !impRoot.activeSelf) return;

        if (impDayMat != null)
        {
            Color c = impDayMat.color; c.a = dayFadeFactor; impDayMat.color = c;
            if (impDayMR != null) impDayMR.enabled = c.a > 0.004f;
        }
        if (impNightMat != null)
        {
            Color c = impNightMat.color; c.a = nightFactor; impNightMat.color = c;
            if (impNightMR != null) impNightMR.enabled = c.a > 0.004f;
        }
    }

    void DestroyForestImposter()
    {
        if (impRoot != null) Destroy(impRoot);
        impRoot  = null;
        impDayMR = null;
        impNightMR = null;
        if (impDayMat   != null) { Destroy(impDayMat);   impDayMat   = null; }
        if (impNightMat != null) { Destroy(impNightMat); impNightMat = null; }
        if (impBakeMat  != null) { Destroy(impBakeMat);  impBakeMat  = null; }
        if (impDayRT    != null) { impDayRT.Release();   Destroy(impDayRT);   impDayRT   = null; }
        if (impNightRT  != null) { impNightRT.Release(); Destroy(impNightRT); impNightRT = null; }
        impBaked = false;
    }

    // -------------------------------------------------------------------------
    // BAKE
    // -------------------------------------------------------------------------

    // Bake edilecek tek ağacın anlık verisi — transform'lara DOKUNMADAN önceden toplanır.
    struct BakeItem
    {
        public Mesh      mesh;       // sprite geometry (cache'li)
        public Matrix4x4 matrix;     // dünya matrisi (tier1 ölçek boost'u uygulanmış)
        public Texture   texture;    // sprite'ın (atlas) dokusu
        public Mesh      nightMesh;  // gece varyantı (yoksa day ile aynı)
        public Matrix4x4 nightMatrix;
        public Texture   nightTexture;
        public int       sortOrder;
    }

    void BakeForestImposter()
    {
        // -- Dünya kapsamı: harita sınırları + kanopi taşma payı --------------------------------
        float mapW = cachedMap.width  / pixelsPerUnit;
        float mapH = cachedMap.height / pixelsPerUnit;
        const float margin = 1.5f; // ağaç sprite'ı tile merkezinden yukarı/yana taşabilir
        float w = mapW + margin * 2f;
        float h = mapH + margin * 2f;
        Vector3 center = transform.position;

        int maxRes = Mathf.Clamp(forestImposterMaxRes, 512, 4096);
        int resX, resY;
        if (w >= h) { resX = maxRes; resY = Mathf.Max(64, Mathf.RoundToInt(maxRes * h / w)); }
        else        { resY = maxRes; resX = Mathf.Max(64, Mathf.RoundToInt(maxRes * w / h)); }

        // Kamera kullanılmadığı için depth buffer gerekmez (render graph kısıtı yok).
        impDayRT   = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32) { name = "ForestImposterDay" };
        impNightRT = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32) { name = "ForestImposterNight" };

        // -- Bake listesi: tier 1 ile BİREBİR aynı ağaç kümesi ve ölçek --------------------------
        // Seyreltilen ağaçlar bake'e girmez, kalanlar aynı ölçek boost'unu alır → tier 1 ↔ tier 2
        // geçişi dikişsiz. Deterministik seyreltme sayesinde küme her zaman aynıdır.
        var meshCache = new Dictionary<Sprite, Mesh>();
        var items     = new List<BakeItem>();
        int maxOrder  = 10;

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (!bd.isTree || bd.go == null || bd.dayRenderer == null || bd.dayRenderer.sprite == null) continue;
            if (IsTreeThinned(bd)) continue;

            Sprite daySp = bd.dayRenderer.sprite;
            float  s     = bd.baseScale > 0f ? bd.baseScale * treeThinScaleBoost
                                             : bd.go.transform.localScale.x;
            Vector3 pos  = bd.go.transform.position;
            var scl      = new Vector3(s, s, 1f);

            var item = new BakeItem
            {
                mesh      = GetSpriteMesh(daySp, meshCache),
                matrix    = Matrix4x4.TRS(pos, Quaternion.identity, scl),
                texture   = daySp.texture,
                sortOrder = bd.dayRenderer.sortingOrder,
            };

            // Gece varyantı: night overlay'in local hizalama ofseti ölçekle çarpılır (child scale=1,
            // parent scale=s olduğu için dünya ofseti = align * s). Gece sprite yoksa gündüz kalır
            // (ApplyCrossfade'in "gece sprite yoksa gündüz tam opak" kuralının bake karşılığı).
            if (bd.nightRenderer != null && bd.nightRenderer.sprite != null)
            {
                Sprite nightSp = bd.nightRenderer.sprite;
                Vector3 align  = bd.nightRenderer.transform.localPosition;
                item.nightMesh    = GetSpriteMesh(nightSp, meshCache);
                item.nightMatrix  = Matrix4x4.TRS(pos + new Vector3(align.x * s, align.y * s, 0f),
                                                  Quaternion.identity, scl);
                item.nightTexture = nightSp.texture;
            }
            else
            {
                item.nightMesh    = item.mesh;
                item.nightMatrix  = item.matrix;
                item.nightTexture = item.texture;
            }

            if (item.sortOrder > maxOrder) maxOrder = item.sortOrder;
            items.Add(item);
        }

        if (items.Count == 0) { DestroyForestImposter(); return; }

        // TEŞHİS: bake kapsamı ile ağaçların gerçek dünya yayılımı örtüşüyor mu? Ağaçlar
        // [center±w/2, center±h/2] dışında kalıyorsa hizalama/pivot sorunu vardır.
        float tMinX = float.MaxValue, tMaxX = float.MinValue, tMinY = float.MaxValue, tMaxY = float.MinValue;
        for (int i = 0; i < items.Count; i++)
        {
            Vector3 p = items[i].matrix.GetColumn(3);
            if (p.x < tMinX) tMinX = p.x; if (p.x > tMaxX) tMaxX = p.x;
            if (p.y < tMinY) tMinY = p.y; if (p.y > tMaxY) tMaxY = p.y;
        }
        Debug.Log($"MapDecorPlacer: imposter bake bounds — quad merkez=({center.x:F2},{center.y:F2}) " +
                  $"kapsam=({w:F2}x{h:F2}) | ağaçlar x∈[{tMinX:F2},{tMaxX:F2}] y∈[{tMinY:F2},{tMaxY:F2}]");

        // Canlı çizimle aynı üst üste binme için arkadan öne (sortingOrder artan) sırala.
        items.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

        // -- CommandBuffer ile ham çizim ----------------------------------------------------------
        if (impBakeMat == null) impBakeMat = new Material(Shader.Find("Sprites/Default"));

        // Ortho projeksiyon: harita kapsamı → RT. GetGPUProjectionMatrix(_, true) platformun RT
        // konvansiyonunu (DX'te dikey flip vb.) uygular.
        Matrix4x4 view = Matrix4x4.Translate(new Vector3(-center.x, -center.y, 0f));
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(
            Matrix4x4.Ortho(-w * 0.5f, w * 0.5f, -h * 0.5f, h * 0.5f, -50f, 50f), true);

        var mpb = new MaterialPropertyBlock();
        var cmd = new CommandBuffer { name = "ForestImposterBake" };

        // Gündüz pası
        cmd.SetRenderTarget(impDayRT);
        cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
        cmd.SetViewProjectionMatrices(view, proj);
        for (int i = 0; i < items.Count; i++)
        {
            mpb.SetTexture("_MainTex", items[i].texture);
            cmd.DrawMesh(items[i].mesh, items[i].matrix, impBakeMat, 0, 0, mpb);
        }

        // Gece pası
        cmd.SetRenderTarget(impNightRT);
        cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
        cmd.SetViewProjectionMatrices(view, proj);
        for (int i = 0; i < items.Count; i++)
        {
            mpb.SetTexture("_MainTex", items[i].nightTexture);
            cmd.DrawMesh(items[i].nightMesh, items[i].nightMatrix, impBakeMat, 0, 0, mpb);
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        // Mesh cache yalnızca bake için gerekliydi.
        foreach (var kvp in meshCache) Destroy(kvp.Value);

        // -- Imposter quad'ları -------------------------------------------------------------------
        // Sıralama: tüm ağaçların üstünde (maxOrder+1/+2). Tier 2'de ağaçlar zaten gizli olduğu
        // için tek önemi diğer dekorla (bina vb.) etkileşim — bu zoom ölçeğinde fark algılanamaz.
        impRoot = new GameObject("ForestImposter");
        impRoot.transform.SetParent(transform, false);
        impRoot.transform.position = new Vector3(center.x, center.y, spriteZ);

        impDayMR   = CreateImposterQuad("Day",   impDayRT,   w, h, maxOrder + 1, out impDayMat);
        impNightMR = CreateImposterQuad("Night", impNightRT, w, h, maxOrder + 2, out impNightMat);

        impRoot.SetActive(false); // SetForestImposterVisible açar
        impBaked = true;

        Debug.Log($"MapDecorPlacer: forest imposter bake — {items.Count} ağaç → {resX}x{resY} x2 RT " +
                  "(CommandBuffer, kamerasız).");
    }

    /// <summary>
    /// Sprite geometrisinden mesh üretir (SpriteRenderer'ın çizdiği vertex/uv/tris ile birebir).
    /// Aynı sprite için cache'lenir — 5-10 ağaç varyantı = 5-10 mesh.
    /// </summary>
    static Mesh GetSpriteMesh(Sprite sp, Dictionary<Sprite, Mesh> cache)
    {
        if (cache.TryGetValue(sp, out var m)) return m;

        Vector2[] sv = sp.vertices;
        var verts = new Vector3[sv.Length];
        for (int i = 0; i < sv.Length; i++) verts[i] = sv[i];

        ushort[] st = sp.triangles;
        var tris = new int[st.Length];
        for (int i = 0; i < st.Length; i++) tris[i] = st[i];

        m = new Mesh { name = "ImposterSprite_" + sp.name };
        m.vertices  = verts;
        m.uv        = sp.uv;
        m.triangles = tris;
        m.RecalculateBounds();

        cache[sp] = m;
        return m;
    }

    MeshRenderer CreateImposterQuad(string name, RenderTexture rt, float w, float h,
                                    int sortingOrder, out Material mat)
    {
        var go = new GameObject("ForestImposter" + name);
        go.transform.SetParent(impRoot.transform, false);

        float hw = w * 0.5f, hh = h * 0.5f;
        var mesh = new Mesh { name = "ForestImposterQuad" };
        mesh.vertices  = new[]
        {
            new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
            new Vector3(hw, hh, 0f),   new Vector3(-hw, hh, 0f),
        };
        // V DİKEY TERS: CommandBuffer ile kamerasız RT'ye yazarken (GetGPUProjectionMatrix(_, true))
        // DX'te satır sırası ekran konvansiyonunun tersine döner → düz UV ile orman dikeyde aynalı
        // çıkıyordu (güneybatı kıyı ağaçları kuzeybatı denizinde görünüyordu). Quad V'yi ters örnekleyerek
        // telafi edilir.
        mesh.uv        = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        // Özel premultiplied display shader — Sprites/Default alpha'yı ikinci kez çarpıp yumuşak
        // kenarları koyulaştırır (bkz. MapForestImposter.shader). Bulunamazsa fallback.
        Shader impShader = Shader.Find("Custom/MapForestImposter");
        if (impShader == null)
            Debug.LogWarning("MapDecorPlacer: Custom/MapForestImposter shader'ı bulunamadı — " +
                             "Sprites/Default fallback kullanılıyor, imposter KOYU görünecek. " +
                             "Shader dosyası import edilmiş mi / Always Included'da mı kontrol et.");
        mat = new Material(impShader != null ? impShader : Shader.Find("Sprites/Default"));
        mat.mainTexture = rt;
        mat.color       = new Color(1f, 1f, 1f, 1f);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder   = sortingOrder;
        return mr;
    }
}
