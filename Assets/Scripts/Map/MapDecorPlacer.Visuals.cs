using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Day/Night crossfade, dynamic shadows, and city-building / shadow GameObject creation.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // DAY / NIGHT CROSSFADE
    // -------------------------------------------------------------------------

    // Tekrar tekrar oluşturmamak için crossfade'de kullanılan geçici Color yapıları
    private Color crossfadeTemp;

    void ApplyCrossfade(float ratio)
    {
        // Gerçek crossfade: gündüz sprite gece sprite'ı solarken SOLAR (eskiden hep tam
        // opak kalıyordu → gece sprite'ının altında görünüp, hizasızlıkta hayalet/blur
        // yapıyordu). Sabit-güç (sqrt) eğrisi: geçiş boyunca toplam kapama yüksek kalır
        // (zemin sızmaz) ama uçlarda yalnızca tek sprite görünür → gece gündüz silueti
        // tamamen kaybolur. Gece sprite YOKSA gündüz tam opak kalır (bina gece kaybolmasın).
        float nightFactor = Mathf.Sqrt(Mathf.Clamp01(ratio));
        float dayFadeFactor = Mathf.Sqrt(Mathf.Clamp01(1f - ratio));

        // DEBUG overlap test: force BOTH sprites visible at a fixed alpha so day/night
        // silhouettes can be compared directly. If they don't line up here, the variant
        // sprites differ in shape/size — placement is already identical.
        if (debugOverlayDayNight)
        {
            nightFactor   = debugOverlayAlpha;
            dayFadeFactor = debugOverlayAlpha;
        }

        // Buildings
        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.dayRenderer == null) continue;

            float baseA    = bd.baseAlpha;
            bool  hasNight = bd.nightRenderer != null;

            crossfadeTemp   = bd.dayRenderer.color;
            crossfadeTemp.a = hasNight ? baseA * dayFadeFactor : baseA;
            bd.dayRenderer.color = crossfadeTemp;

            if (hasNight)
            {
                crossfadeTemp   = bd.nightRenderer.color;
                crossfadeTemp.a = baseA * nightFactor;
                bd.nightRenderer.color = crossfadeTemp;
            }
        }

        // Ports
        for (int i = 0; i < ports.Count; i++)
        {
            PortData pd = ports[i];
            if (pd.dayRenderer == null) continue;

            float baseA    = pd.baseAlpha;
            bool  hasNight = pd.nightRenderer != null;

            crossfadeTemp   = pd.dayRenderer.color;
            crossfadeTemp.a = hasNight ? baseA * dayFadeFactor : baseA;
            pd.dayRenderer.color = crossfadeTemp;

            if (hasNight)
            {
                crossfadeTemp   = pd.nightRenderer.color;
                crossfadeTemp.a = baseA * nightFactor;
                pd.nightRenderer.color = crossfadeTemp;
            }
        }

        // Ships
        for (int i = 0; i < activeShips.Count; i++)
        {
            ShipInstance ship = activeShips[i];
            if (ship.dayRenderer == null) continue;

            float baseA    = ship.baseAlpha;
            bool  hasNight = ship.nightRenderer != null;

            crossfadeTemp   = ship.dayRenderer.color;
            crossfadeTemp.a = hasNight ? baseA * dayFadeFactor : baseA;
            ship.dayRenderer.color = crossfadeTemp;

            if (hasNight)
            {
                crossfadeTemp   = ship.nightRenderer.color;
                crossfadeTemp.a = baseA * nightFactor;
                ship.nightRenderer.color = crossfadeTemp;
            }
        }
    }

    // -------------------------------------------------------------------------
    // DYNAMIC SHADOW — Güneş pozisyonuna göre gölge güncelleme
    // -------------------------------------------------------------------------

    void UpdateShadows(float sunProgress)
    {
        bool isNight = sunProgress < 0f;

        // Gündüz → alpha 1, gece → alpha 0. Şafak/akşam geçişinde smoothstep ile fade.
        float lightingRatio = (dayNight != null) ? dayNight.LightingRatio : 0f;
        float alphaFactor = 1f - lightingRatio;

        // -- Süpürme yeniden eşlemesi --------------------------------------------------
        // SORUN: SunProgress güneş yayını dawn+day+dusk süresine LİNEER yayar. Gündüz fazı
        // bu yayda küçük bir merkezi dilimdir (ör. dawn5+day2+dusk5 → gündüz yalnızca
        // SunProgress [0.42, 0.58]). Yani gündüz güneş neredeyse tepe noktasında → gölge ne
        // döner ne uzar. Tüm hareket uzun şafak/akşama düşer.
        //
        // ÇÖZÜM: Yayı yeniden eşle — toplam açısal süpürmenin 'daytimeSweepFraction' kadarı
        // GÜNDÜZ fazına ayrılsın. Gündüz penceresi [a,b] → sweep01 [0.5-half, 0.5+half]'e
        // genişler; şafak/akşam kalan uçlara sıkışır. sweep01 hem yön hem yükseklik (altitude)
        // sürer → gündüz gölgeleri belirgin döner VE uzar; şafak/akşam yavaşlar.
        // daytimeSweepFraction=0 → eski lineer davranış.
        float sweep01;
        if (dayNight != null && daytimeSweepFraction > 0f)
        {
            float a    = dayNight.SunDayStart;
            float b    = dayNight.SunDayEnd;

            // KRİTİK: daytimeSweepFraction gündüze ayrılan sweep payıdır. Gündüzün GERÇEK
            // zaman payı (b-a) bundan büyükse, küçük bir kesir gündüzü minik bir sweep
            // dilimine SIKIŞTIRIR → gölge gün boyunca neredeyse hiç dönmez, tüm hareket
            // 0.1sn'lik şafak/akşama sıçrar (gölge gündüz DONAR). Bunu önlemek için payı
            // gündüzün doğal genişliğinin ALTINA düşürmeyiz: kesir yalnızca gündüzü
            // GENİŞLETEBİLİR, asla sıkıştıramaz. Doğal genişliğe eşit → saf lineer süpürme
            // (tüm gün boyunca düzgün, sürekli hareket).
            float naturalDay = Mathf.Clamp01(b - a);
            float frac = Mathf.Max(Mathf.Clamp01(daytimeSweepFraction), naturalDay);
            float half = frac * 0.5f;
            float lo   = 0.5f - half;
            float hi   = 0.5f + half;

            // Üç doğrusal süpürme segmenti. Knot'larda (a, b) ÇAKIŞIRLAR (lo, hi) → eğri
            // SÜREKLİ; ama eğimleri (= süpürme hızı) farklı → knot'ta hız ANİ sıçrar.
            float dawnLine = (a > 1e-4f) ? Mathf.Lerp(0f, lo, sunProgress / a) : lo;
            float dayLine  = Mathf.Lerp(lo, hi, (sunProgress - a) / Mathf.Max(1e-4f, b - a));
            float duskLine = Mathf.Lerp(hi, 1f, (sunProgress - b) / Mathf.Max(1e-4f, 1f - b));

            // HIZI yumuşat: segmentler arası geçişi knot'un ETRAFINDA smoothstep ile harmanla
            // → eğim (hız) sürekli değişir, ani köşe yok. Geçiş knot'ta MERKEZLİ (faz sınırı =
            // gündüz başı/sonu) ve bant komşu fazların kısasından türer → cycle'a senkron,
            // asla komşu fazı aşmaz. smoothing=0 → sert knot (eski davranış).
            float k     = Mathf.Clamp01(shadowSpeedSmoothing);
            float bandA = 0.5f * k * Mathf.Min(a,      b - a);
            float bandB = 0.5f * k * Mathf.Min(1f - b, b - a);
            float sA = (bandA > 1e-5f) ? Mathf.SmoothStep(0f, 1f, (sunProgress - (a - bandA)) / (2f * bandA))
                                       : (sunProgress >= a ? 1f : 0f);
            float sB = (bandB > 1e-5f) ? Mathf.SmoothStep(0f, 1f, (sunProgress - (b - bandB)) / (2f * bandB))
                                       : (sunProgress >= b ? 1f : 0f);

            sweep01 = Mathf.Lerp(Mathf.Lerp(dawnLine, dayLine, sA), duskLine, sB);
        }
        else
        {
            sweep01 = Mathf.Clamp01(sunProgress);
        }

        // Gölge yönü: -1=batı (sabah, güneş doğuda), 0=öğle, +1=doğu (akşam, güneş batıda)
        float shadowDir = sweep01 * 2f - 1f;
        float dirSign   = shadowDir >= 0f ? 1f : -1f;

        // Güneş yükseklik faktörü (altitude, 0-1): parabolik yörünge, şafak 0 → öğle 1 → akşam 0
        float altitude = Mathf.Sin(Mathf.PI * sweep01);

        // Gölge uzunluğu — yükseklikle SÜREKLİ değişir (donma platosu / clamp köşesi YOK).
        // ESKİ: length = height/tan(elev) + Clamp(min,max). 1/tan alçak güneşte tavana ÇARPIP
        // DONUYORDU → sweep [0,0.3] ve [0.7,1] boyunca uzunluk SABİT kalıyor (gölge "yavaş"),
        // sonra clamp'ten çıkınca hız 0 → yüksek ANİ sıçrıyor (= "ani/süreksiz hız değişimi").
        // ŞİMDİ: altitude → uzunluk düz (C1) harman; tavan/taban shadowMaxLength/shadowMidScale,
        // shadowHeightRatio eğriyi şekillendirir (büyük = uzun gölge daha uzun süre korunur).
        // Hiçbir noktada clamp'e çarpmadığı için gölge tüm süpürme boyunca kesintisiz uzar/kısalır.
        float lenT = Mathf.Pow(altitude, shadowHeightRatio);
        float lengthFactor = Mathf.Lerp(shadowMaxLength, shadowMidScale, lenT);

        Color col = shadowColor;
        col.a = shadowColor.a * alphaFactor;

        for (int i = 0; i < cityBuildings.Count; i++)
        {
            BuildingData bd = cityBuildings[i];
            if (bd.shadow == null || bd.shadow.transform == null) continue;
            ShadowHandle sh = bd.shadow;

            if (isNight || alphaFactor <= 0.001f)
            {
                sh.transform.gameObject.SetActive(false);
                continue;
            }

            sh.transform.gameObject.SetActive(true);

            if (sh.isIsometric)
            {
                // Iso branch — pivot sprite tabanına kaydırılır, gölge diyagonal uzar.
                // Uzunluk halfHeight üzerinden hesaplanır (iso sprite yüksekliği bina perspektif yüksekliğini kodlar).
                // Kalınlık halfWidth üzerinden (sprite genişliği binanın taban genişliğine yakın).
                // Kendi parametre seti (isoShadowMaxLength/isoShadowNoonDeadZone) kullanılır — flat moddan bağımsız.
                //
                // SÜREKLİ öğlen geçişi (DONMAZ). Öğlende dirSign anlık -1 → +1 flip yapar; bunu
                // gizlemek için gölge öğle noktasına yaklaşırken küçülür. ESKİDEN: |shadowDir| bir
                // eşiğin (deadZone) altındayken t SABİT kalıyordu → gölge günün ortasında bir BANT
                // boyunca aynı boy/skew'de DONUYORDU (hareket duruyordu).
                //
                // ŞİMDİ: t, |shadowDir| ile SÜREKLİ artar (düz plato yok). Yalnızca tam öğle ANINDA
                // minimuma (isoShadowNoonMinScale) iner, oradan itibaren hemen büyür → gölge gün
                // boyunca kesintisiz döner+uzar. isoShadowNoonDeadZone artık "öğle kıstırma genişliği":
                // küçük = keskin/dar kıstırma, büyük = geniş yumuşak kıstırma.
                float absDir = Mathf.Abs(shadowDir);
                float blend  = Mathf.Max(0.0001f, isoShadowNoonDeadZone);
                float taper  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(absDir / blend));
                float t      = isoShadowNoonMinScale + (1f - isoShadowNoonMinScale) * taper;

                // Safety guard: t neredeyse sıfır → mesh tamamen gizlenir (yalnızca minScale=0 iken öğle anı).
                if (t < 0.001f)
                {
                    sh.transform.gameObject.SetActive(false);
                    continue;
                }

                // Flat dalla aynı düzeltme: 1/tan + Min(maxLength) alçak güneşte tavana ÇARPIP
                // DONUYORDU (gölge boyu sabit → "yavaş", sonra clamp'ten çıkınca ANİ hız sıçraması).
                // ŞİMDİ altitude → boy düz (C1) harman: ufukta isoShadowMaxLength, öğlede ~0 →
                // donma platosu/köşe yok, gölge tüm süpürme boyunca kesintisiz uzar/kısalır.
                float isoLengthFactor = Mathf.Lerp(isoShadowMaxLength, 0f, Mathf.Pow(altitude, isoShadowHeightRatio));
                isoLengthFactor *= t;

                // Pivot Y: sprite alt kenarından yukarı doğru ofset (binanın yere değdiği görsel nokta).
                // Pivot X: izometrik footprint sprite ortasında — sabah/akşam kayma yok, hep merkezde.
                float baseY = -sh.halfHeight + sh.halfHeight * 2f * isoShadowPivotOffsetRatio;
                sh.transform.localPosition = new Vector3(0f, baseY, 0f);
                sh.transform.localScale    = new Vector3(dirSign, 1f, 1f);

                // Iso trapez:
                //   isoNear  = halfWidth * scale * t → taban kalınlığı, t ile birlikte küçülür → öğlen 0
                //   isoLength = halfHeight * factor → uzunluk (bina yüksekliğinin gölgeye izdüşümü)
                float isoNear   = sh.halfWidth  * isoShadowNearScale * t;
                float isoFar    = isoNear * shadowTipRatio;
                float isoLength = sh.halfHeight * isoLengthFactor;

                // Iso skew: uzak uç -Y'ye doğru kayar → diyagonal görünüm.
                float isoRad = isoShadowAngleDegrees * Mathf.Deg2Rad;
                float skew   = isoLength * Mathf.Tan(isoRad) * 0.5f;

                sh.verts[0] = new Vector3(0f,        -isoNear,           0f);
                sh.verts[1] = new Vector3(0f,        +isoNear,           0f);
                sh.verts[2] = new Vector3(isoLength, +isoFar - skew,     0f);
                sh.verts[3] = new Vector3(isoLength, -isoFar - skew,     0f);
                sh.mesh.vertices = sh.verts;
                sh.mesh.RecalculateBounds();
            }
            else
            {
                // Flat branch — mevcut algoritma, sprite merkezinden yatay trapez.
                // Pivot = gölgenin binaya bitişik kenarı = binanın güneş karşı taban kenarı.
                // shadowDir=-1 (sabah): pivot binanın sol kenarı (-hw), gölge sola uzanır.
                // shadowDir=+1 (akşam): pivot binanın sağ kenarı (+hw), gölge sağa uzanır.
                // Container scale.x = dirSign: mesh +x yönünde uzanır, flip ile diğer tarafa yansır.
                sh.transform.localPosition = new Vector3(shadowDir * sh.halfWidth, 0f, 0f);
                sh.transform.localScale    = new Vector3(dirSign, 1f, 1f);

                // Trapez vertex güncellemesi — yakın uç tam bina kenarı, uzak uç tipRatio'ya göre incelmiş.
                // Uzunluk = binanın tam genişliği (halfWidth*2) × lengthFactor.
                float near   = sh.halfHeight * shadowNearScale;
                float far    = near * shadowTipRatio;
                float length = sh.halfWidth * 2f * lengthFactor;

                sh.verts[0] = new Vector3(0f,     -near, 0f);
                sh.verts[1] = new Vector3(0f,     +near, 0f);
                sh.verts[2] = new Vector3(length, +far, 0f);
                sh.verts[3] = new Vector3(length, -far, 0f);
                sh.mesh.vertices = sh.verts;
                sh.mesh.RecalculateBounds();
            }

            // Alpha fade — şafakta yavaş yavaş oluşur, akşamda yavaş yavaş kaybolur
            if (sh.material != null)
                sh.material.color = col;
        }
    }

    // -------------------------------------------------------------------------
    // CITY BUILDING / SHADOW GAMEOBJECT CREATION
    // -------------------------------------------------------------------------

    // Gece sprite'ını gündüz sprite'ının üstüne hizalar.
    // Sorun: sprite'lar opak piksel sınırına otomatik dilimlenmiş; gece varyantında pencere/çatı
    // ışığı dışarı taştığı için bounding box DAHA BÜYÜK çıkar (ör. sky 98px, skynight 105px → +7px
    // tepede). Eski yöntem (bounds.center) iki MERKEZİ çakıştırdığı için fazla ışık binayı aşağı
    // kaydırıyordu. Bina YERE BASTIĞI için yatayda merkez, dikeyde ALT KENAR referans alınır →
    // bina tabanı sabit kalır, ışık serbestçe yukarı taşar.
    static Vector3 ComputeDayNightAlign(Sprite daySprite, Sprite nightSprite)
    {
        return new Vector3(
            daySprite.bounds.center.x - nightSprite.bounds.center.x, // yatay: merkez
            daySprite.bounds.min.y    - nightSprite.bounds.min.y,    // dikey: alt kenar (taban)
            0f);
    }

    (GameObject go, SpriteRenderer daySR, SpriteRenderer nightSR, ShadowHandle shadow) CreateCityBuildingObject(
        Sprite daySprite, Sprite nightSprite, float wx, float wy,
        float scale, float baseA, int sortOrder, bool isIsometric)
    {
        GameObject go = new GameObject("CityBuilding");
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        // Gölge — trapez mesh. UpdateShadows her frame vertex + pozisyon günceller.
        // isIsometric=true ise pivot/UV/yön izometrik moda göre ayarlanır.
        ShadowHandle shadow = AddShadow(go, daySprite, sortOrder, isIsometric);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.flipX        = false;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (nightSprite != null)
        {
            GameObject nightGo = new GameObject("NightOverlay");
            nightGo.transform.SetParent(go.transform, false);
            // Gece sprite'ını gündüz tabanına hizala (bkz. ComputeDayNightAlign — ışık taşması
            // bounding box'ı büyüttüğü için merkez yerine alt kenar referans alınır).
            Vector3 align = ComputeDayNightAlign(daySprite, nightSprite);
            nightGo.transform.localPosition = new Vector3(align.x, align.y, 0f);
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = nightSprite;
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.flipX        = false;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        return (go, daySR, nightSR, shadow);
    }

    ShadowHandle AddShadow(GameObject parent, Sprite sprite, int sortOrder, bool isIsometric)
    {
        // Container — bina merkezinde sabit; UpdateShadows localPosition + scale.x (flip) günceller.
        GameObject containerGo = new GameObject("Shadow");
        containerGo.transform.SetParent(parent.transform, false);
        containerGo.transform.localPosition = Vector3.zero;
        containerGo.transform.localScale    = Vector3.one;
        containerGo.transform.localRotation = Quaternion.identity;

        float halfWidth  = sprite.rect.width  / sprite.pixelsPerUnit * 0.5f;
        float halfHeight = sprite.rect.height / sprite.pixelsPerUnit * 0.5f;

        // Başlangıç vertex'leri — UpdateShadows zaten her frame yeniden yazıyor, sadece bounds için.
        // Iso modda kalınlık halfWidth, uzunluk halfHeight üzerinden hesaplanır.
        Vector3[] verts = new Vector3[4];
        if (isIsometric)
        {
            float startNear   = halfWidth  * isoShadowNearScale;
            float startLength = halfHeight * isoShadowMaxLength;
            verts[0] = new Vector3(0f,           -startNear, 0f);
            verts[1] = new Vector3(0f,           +startNear, 0f);
            verts[2] = new Vector3(startLength,  +startNear * shadowTipRatio, 0f);
            verts[3] = new Vector3(startLength,  -startNear * shadowTipRatio, 0f);
        }
        else
        {
            verts[0] = new Vector3(0f,              -halfHeight, 0f);
            verts[1] = new Vector3(0f,              +halfHeight, 0f);
            verts[2] = new Vector3(halfWidth * 2f,  +halfHeight * shadowTipRatio, 0f);
            verts[3] = new Vector3(halfWidth * 2f,  -halfHeight * shadowTipRatio, 0f);
        }

        // UV — bina sprite atlas rect'i.
        // Flat modda tüm sprite UV'si gölgeye basılır.
        // Iso modda sadece sprite'ın alt bandı (taban kısmı) sample edilir → gölgeye binanın
        // üst yüzeyi/çatı sızmaz, sadece taban silueti düşer.
        Rect r   = sprite.rect;
        float tw = sprite.texture.width;
        float th = sprite.texture.height;
        Vector2 uvMin = new Vector2(r.x / tw, r.y / th);
        Vector2 uvMax = new Vector2((r.x + r.width) / tw, (r.y + r.height) / th);

        Vector2[] uvs = new Vector2[4];
        if (isIsometric)
        {
            // Iso UV: sprite 90° döndürülerek trapez'e basılır.
            //   Sprite ALT kenarı  → trapez yakın kenarı (binaya bitişik) → silueti binanın "ayağı"ndan başlatır.
            //   Sprite ÜST kenarı  → trapez uzak ucu                    → bina silueti uzar gibi yansır.
            // Şeffaf piksel alanları gölgede de şeffaf kalır → gölge binanın silueti şeklinde görünür.
            uvs[0] = new Vector2(uvMin.x, uvMin.y); // near-bottom = sprite bottom-left
            uvs[1] = new Vector2(uvMax.x, uvMin.y); // near-top    = sprite bottom-right
            uvs[2] = new Vector2(uvMax.x, uvMax.y); // far-top     = sprite top-right
            uvs[3] = new Vector2(uvMin.x, uvMax.y); // far-bottom  = sprite top-left
        }
        else
        {
            // Flat UV: sprite trapez'e olduğu yönde basılır (sol→sağ).
            uvs[0] = new Vector2(uvMin.x, uvMin.y);
            uvs[1] = new Vector2(uvMin.x, uvMax.y);
            uvs[2] = new Vector2(uvMax.x, uvMax.y);
            uvs[3] = new Vector2(uvMax.x, uvMin.y);
        }

        int[] tris = new int[] { 0, 1, 2, 0, 2, 3 };

        Mesh mesh = new Mesh();
        mesh.name = "ShadowTrapezoid";
        mesh.MarkDynamic();
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        MeshFilter mf = containerGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = containerGo.AddComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = sprite.texture;
        mat.color       = shadowColor;
        mr.sharedMaterial = mat;
        mr.sortingOrder   = sortOrder - 1;

        return new ShadowHandle
        {
            transform   = containerGo.transform,
            renderer    = mr,
            mesh        = mesh,
            material    = mat,
            verts       = verts,
            halfWidth   = halfWidth,
            halfHeight  = halfHeight,
            isIsometric = isIsometric,
        };
    }

    GameObject PlaceSimpleSprite(string goName, Sprite sprite, float wx, float wy,
                                float scale, bool flipX, int sortOrder)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder;
        sr.flipX        = flipX;
        sr.color        = new Color(1f, 1f, 1f, Random.Range(0.85f, 1f));
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        decorObjects.Add(go);
        return go;
    }
}
