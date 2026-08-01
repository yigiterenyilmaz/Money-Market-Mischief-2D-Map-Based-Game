using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ağaç için pan/zoom. Eski TreePanZoom'dan farkları:
/// zoom imlecin altındaki noktayı sabit tutar ve içerik görüş alanının dışına kaçamaz.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkillTreeViewport : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("Zoom")]
    public float zoomStep = 0.12f;
    [Tooltip("Daha fazla uzaklaşmak yazıları okunmaz hale getiriyor")]
    public float minZoom = 0.45f;
    public float maxZoom = 1.6f;
    public float zoomLerpSpeed = 16f;

    [Header("Pan")]
    [Tooltip("İçeriğin görüş alanı kenarından dışarı taşabileceği pay. " +
             "Büyük tutulursa kök node'un solunda boş alan görünür.")]
    public float panMargin = 40f;
    [Tooltip("Kısayolla ağaca giderken kameranın kayma hızı — yüksek değer daha çabuk varır")]
    public float panLerpSpeed = 13f;

    private RectTransform viewport;
    private RectTransform content;
    private float currentZoom = 1f;
    private float targetZoom = 1f;

    public float Zoom => currentZoom;

    public void Bind(RectTransform contentRect)
    {
        viewport = GetComponent<RectTransform>();
        content = contentRect;
        currentZoom = targetZoom = content.localScale.x;
    }

    public void SetZoom(float zoom, bool immediate = false)
    {
        targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        if (!immediate) return;

        currentZoom = targetZoom;
        content.localScale = new Vector3(currentZoom, currentZoom, 1f);
        content.anchoredPosition = ClampPosition(content.anchoredPosition);
    }

    /// <summary>
    /// Verilen içerik noktasını ekranın ortasına alır.
    /// animate=true ise ışınlanmak yerine hızlıca kayar — kullanıcı nereye gittiğini takip edebilsin.
    /// </summary>
    public void CenterOn(Vector2 contentPoint, bool animate = false)
    {
        //hedef, konum değil İÇERİK NOKTASI olarak saklanır: zoom da animasyonluysa
        //hedef konum her karede değişir, sabit konum saklamak sonunda kaymaya yol açardı
        panPoint = contentPoint;

        if (animate)
        {
            panning = true;
            return;
        }

        content.anchoredPosition = ClampPosition(-contentPoint * currentZoom);
        panning = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null) return;

        //kullanıcı sürüklemeye başladıysa otomatik kaymayı bırak
        panning = false;
        content.anchoredPosition = ClampPosition(content.anchoredPosition + eventData.delta);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null) return;

        float scroll = eventData.scrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        float previous = targetZoom;
        targetZoom = Mathf.Clamp(targetZoom * (1f + Mathf.Sign(scroll) * zoomStep), minZoom, maxZoom);
        if (Mathf.Approximately(previous, targetZoom)) return;

        //zoom hedefi değiştiğinde kayma hedefi geçersizleşir
        panning = false;

        //imlecin altındaki içerik noktasını sabit tut
        Vector2 pointerInViewport;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventData.pressEventCamera, out pointerInViewport))
            return;

        pivotPoint = (pointerInViewport - content.anchoredPosition) / currentZoom;
        pivotScreen = pointerInViewport;
        hasPivot = true;
    }

    private Vector2 pivotPoint;
    private Vector2 pivotScreen;
    private bool hasPivot;
    private Vector2 panPoint;
    private bool panning;

    private void LateUpdate()
    {
        if (content == null) return;

        if (!Mathf.Approximately(currentZoom, targetZoom))
        {
            float t = 1f - Mathf.Exp(-zoomLerpSpeed * Time.unscaledDeltaTime);
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, t);
            if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
                currentZoom = targetZoom;

            content.localScale = new Vector3(currentZoom, currentZoom, 1f);

            if (hasPivot)
                content.anchoredPosition = pivotScreen - pivotPoint * currentZoom;

            content.anchoredPosition = ClampPosition(content.anchoredPosition);
        }
        else
        {
            hasPivot = false;
        }

        if (!panning) return;

        //hedef her karede güncel zoom'a göre yeniden hesaplanır
        Vector2 target = ClampPosition(-panPoint * currentZoom);

        //üstel yumuşatma: mesafe ne olursa olsun aynı sürede varır, uzak ağaçlar da çabuk gelir
        float step = 1f - Mathf.Exp(-panLerpSpeed * Time.unscaledDeltaTime);
        Vector2 next = Vector2.Lerp(content.anchoredPosition, target, step);

        //zoom hâlâ hareket ediyorsa hedef de kayıyor demektir — ikisi de oturmadan bitirme
        bool zoomSettled = Mathf.Approximately(currentZoom, targetZoom);

        if (zoomSettled && (next - target).sqrMagnitude < 1f)
        {
            next = target;
            panning = false;
        }

        content.anchoredPosition = ClampPosition(next);
    }

    /// <summary>Verilen konumu, içerik görüş alanından tamamen çıkmayacak şekilde sınırlar.</summary>
    private Vector2 ClampPosition(Vector2 position)
    {
        if (viewport == null || content == null) return position;

        Vector2 viewSize = viewport.rect.size;
        Vector2 contentSize = content.rect.size * currentZoom;

        if (contentSize.x >= viewSize.x)
        {
            float limitX = (contentSize.x - viewSize.x) * 0.5f + panMargin;
            position.x = Mathf.Clamp(position.x, -limitX, limitX);
        }
        else
        {
            //Ağaç ekrandan dar kaldığında: sola yaslı dur ama TEK bir konuma çakılma.
            //Tek konuma sabitlemek, imlece göre zoom'u eziyor ve kamera sağa yaklaşırken
            //sola sıçrıyordu. Bunun yerine içeriğin ekran dışına taşmadığı aralığa sınırlanır.
            float leftAligned = (contentSize.x - viewSize.x) * 0.5f + panMargin;
            float rightAligned = (viewSize.x - contentSize.x) * 0.5f;
            position.x = Mathf.Clamp(position.x, leftAligned, Mathf.Max(leftAligned, rightAligned));
        }

        float limitY = Mathf.Max(0f, (contentSize.y - viewSize.y) * 0.5f) + panMargin;
        position.y = Mathf.Clamp(position.y, -limitY, limitY);
        return position;
    }
}
