using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ağaçtaki tüm bağlantı çizgilerini TEK mesh olarak çizer.
/// 83 kenar için 83 ayrı Image kullanmak yerine tek draw call — pan/zoom akıcı kalır.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SkillTreeConnectors : MaskableGraphic
{
    private readonly List<Edge> edges = new List<Edge>();
    private SkillTreeStyle style;
    private bool glowPass;
    private CanvasGroup glowGroup;

    public class Edge
    {
        public SkillNodeView from;
        public SkillNodeView to;
        public float progress = 1f; //0..1 — çizginin ne kadarı çizilecek
        public bool animating;
    }

    /// <summary>
    /// glow=true olan kopya sadece AÇILMIŞ kenarları kalın ve saydam çizer, ana çizginin altında durur.
    /// Nabız efekti CanvasGroup.alpha ile yapılır — mesh her karede yeniden kurulmaz.
    /// </summary>
    public void Setup(SkillTreeStyle treeStyle, bool glow = false)
    {
        style = treeStyle;
        glowPass = glow;
        raycastTarget = false;

        if (!glowPass) return;

        glowGroup = gameObject.AddComponent<CanvasGroup>();
        glowGroup.blocksRaycasts = false;
        glowGroup.interactable = false;
    }

    /// <summary>Bir kenarın çizimi node'a ulaştığında haber verir (hedef node bilgilendirilir).</summary>
    public System.Action<SkillNodeView> OnEdgeReached;

    private void Update()
    {
        AdvanceEdgeAnimations();

        if (!glowPass || glowGroup == null || style == null) return;
        if (style.edgeGlowPulseSpeed <= 0f)
        {
            glowGroup.alpha = 1f;
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * style.edgeGlowPulseSpeed) + 1f) * 0.5f;
        glowGroup.alpha = 1f - style.edgeGlowPulseAmount * wave;
    }

    /// <summary>
    /// Verilen node'dan çıkan kenarları sıfırdan çizdirmeye başlar —
    /// skill açılınca çizgi komşulara doğru ilerler.
    /// </summary>
    public void AnimateEdgesFrom(SkillNodeView node)
    {
        bool any = false;

        for (int i = 0; i < edges.Count; i++)
        {
            if (edges[i].from != node) continue;

            edges[i].progress = 0f;
            edges[i].animating = true;
            any = true;
        }

        if (any) SetVerticesDirty();
    }

    private void AdvanceEdgeAnimations()
    {
        //Kenar nesneleri ana katman ile parlama katmanı ARASINDA paylaşılıyor.
        //İkisi de ilerletirse animasyon iki kat hızlanır ve daha kötüsü: hangi katman
        //bitiş karesinde önce çalışırsa 'animating' bayrağını o söndürür. Parlama katmanı
        //önce davranırsa OnEdgeReached hiç tetiklenmez — halka animasyonu bazen çıkıp
        //bazen çıkmamasının sebebi buydu. İlerletmeyi tek katman yapar.
        if (glowPass) return;

        if (style == null || edges.Count == 0) return;

        float duration = Mathf.Max(0.05f, style.edgeDrawDuration);
        bool dirty = false;

        for (int i = 0; i < edges.Count; i++)
        {
            Edge e = edges[i];
            if (!e.animating) continue;

            e.progress += Time.unscaledDeltaTime / duration;
            dirty = true;

            if (e.progress < 1f) continue;

            e.progress = 1f;
            e.animating = false;
            OnEdgeReached?.Invoke(e.to);
        }

        if (dirty) SetVerticesDirty();
    }

    public void SetEdges(IEnumerable<Edge> newEdges)
    {
        edges.Clear();
        edges.AddRange(newEdges);
        SetVerticesDirty();
    }

    /// <summary>Dikey modda ağaç yukarıdan aşağı büyür; dirsekler de o eksende çizilir.</summary>
    public void SetVertical(bool value)
    {
        vertical = value;
        SetVerticesDirty();
    }

    private bool vertical;

    public void Refresh()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (style == null || edges.Count == 0)
            return;

        float half = style.nodeSize * 0.5f + style.lineNodeGap;
        float thickness = style.lineThickness;

        for (int i = 0; i < edges.Count; i++)
        {
            Edge e = edges[i];
            if (e.from == null || e.to == null) continue;

            bool unlocked = e.to.State == SkillNodeState.Unlocked;

            //parlama katmanı sadece açılmış zinciri çizer
            if (glowPass && !unlocked) continue;

            //yatayda soldan sağa, dikeyde yukarıdan aşağı çıkış/giriş yapılır
            Vector2 offset = vertical ? new Vector2(0f, half) : new Vector2(half, 0f);
            Vector2 a = e.from.Rect.anchoredPosition - (vertical ? offset : -offset);
            Vector2 b = e.to.Rect.anchoredPosition + (vertical ? offset : -offset);

            BuildElbowPath(a, b);

            if (glowPass)
            {
                Color glow = style.unlocked.ring;
                glow.a = style.edgeGlowAlpha;
                DrawPolyline(vh, e.progress, thickness * style.edgeGlowWidth, glow);
                continue;
            }

            DrawPolyline(vh, e.progress, unlocked ? thickness * 1.4f : thickness, ColorFor(e));
        }
    }

    /// <summary>
    /// Kenar rengi ilerlemeyi gösterir: alınmış zincir parlak, açılabilir olan vurgulu,
    /// erişilemeyen sönük.
    /// </summary>
    private Color ColorFor(Edge e)
    {
        if (e.to.State == SkillNodeState.Unlocked)
            return style.unlocked.line;

        if (e.to.State == SkillNodeState.Blocked)
            return style.blocked.line;

        if (e.from.State == SkillNodeState.Unlocked)
            return e.to.State == SkillNodeState.Available ? style.available.line : style.unaffordable.line;

        return style.locked.line;
    }

    private readonly List<Vector2> path = new List<Vector2>();

    /// <summary>
    /// Dirseği nokta dizisi olarak kurar (yaylar da noktalara ayrıştırılır).
    /// Kesirli çizim yapabilmek için önce yol çıkarılır, sonra uzunluğa göre kırpılır.
    /// </summary>
    private void BuildElbowPath(Vector2 a, Vector2 b)
    {
        path.Clear();
        path.Add(a);

        const float epsilon = 0.5f;

        if (vertical)
        {
            if (Mathf.Abs(a.x - b.x) < epsilon) { path.Add(b); return; }

            float midY = a.y + (b.y - a.y) * 0.5f;
            float dx = b.x - a.x;
            float radius = Mathf.Max(0f, Mathf.Min(style.lineCornerRadius,
                Mathf.Abs(midY - a.y), Mathf.Abs(dx) * 0.5f));
            int sign = dx > 0f ? 1 : -1;

            path.Add(new Vector2(a.x, midY + radius));
            AppendArc(new Vector2(a.x + radius * sign, midY + radius), radius,
                      90f + 90f * sign, 90f + 180f * sign);
            path.Add(new Vector2(b.x - radius * sign, midY));
            AppendArc(new Vector2(b.x - radius * sign, midY - radius), radius,
                      90f, 90f - 90f * sign);
            path.Add(b);
            return;
        }

        if (Mathf.Abs(a.y - b.y) < epsilon) { path.Add(b); return; }

        float midX = a.x + (b.x - a.x) * 0.5f;
        float dy = b.y - a.y;
        float r = Mathf.Max(0f, Mathf.Min(style.lineCornerRadius,
            Mathf.Abs(midX - a.x), Mathf.Abs(dy) * 0.5f));
        int s = dy > 0f ? 1 : -1;

        path.Add(new Vector2(midX - r, a.y));
        AppendArc(new Vector2(midX - r, a.y + r * s), r, -90f * s, 0f);
        path.Add(new Vector2(midX, b.y - r * s));
        AppendArc(new Vector2(midX + r, b.y - r * s), r, 180f, 180f - 90f * s);
        path.Add(b);
    }

    /// <summary>Yayı noktalara ayrıştırıp yola ekler (ilk nokta zaten yolda olduğu için atlanır).</summary>
    private void AppendArc(Vector2 center, float radius, float fromDeg, float toDeg)
    {
        if (radius <= 0.01f) return;

        const int steps = 6;
        for (int i = 1; i <= steps; i++)
            path.Add(center + Polar(Mathf.Lerp(fromDeg, toDeg, (float)i / steps), radius));
    }

    /// <summary>
    /// Yolu baştan itibaren toplam uzunluğun `fraction` kadarını çizer.
    /// fraction = 1 ise tamamı; animasyonda çizgi node'dan node'a doğru uzar.
    /// </summary>
    private void DrawPolyline(VertexHelper vh, float fraction, float thickness, Color color)
    {
        if (path.Count < 2) return;

        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0f) return;

        float total = 0f;
        for (int i = 1; i < path.Count; i++)
            total += Vector2.Distance(path[i - 1], path[i]);

        if (total <= 0.01f) return;

        float target = total * fraction;
        float travelled = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 from = path[i - 1];
            Vector2 to = path[i];
            float length = Vector2.Distance(from, to);
            if (length <= 0.0001f) continue;

            if (travelled + length <= target)
            {
                AddSegment(vh, from, to, thickness, color);
                travelled += length;
                continue;
            }

            //son parça: hedefe kalan kadarını çiz ve bırak
            float remaining = target - travelled;
            if (remaining > 0.01f)
                AddSegment(vh, from, from + (to - from) / length * remaining, thickness, color);

            return;
        }
    }

    private static Vector2 Polar(float degrees, float radius)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
    }

    private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.01f) return;

        dir /= length;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        //uçları yarım kalınlık uzatınca köşeler boşluksuz birleşir
        Vector2 extend = dir * (thickness * 0.5f);
        Vector2 p0 = a - extend;
        Vector2 p1 = b + extend;

        UIVertex[] quad = new UIVertex[4];
        quad[0] = Vertex(p0 - normal, color);
        quad[1] = Vertex(p0 + normal, color);
        quad[2] = Vertex(p1 + normal, color);
        quad[3] = Vertex(p1 - normal, color);

        vh.AddUIVertexQuad(quad);
    }

    private static UIVertex Vertex(Vector2 position, Color color)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = position;
        v.color = color;
        v.uv0 = Vector2.zero;
        return v;
    }
}
