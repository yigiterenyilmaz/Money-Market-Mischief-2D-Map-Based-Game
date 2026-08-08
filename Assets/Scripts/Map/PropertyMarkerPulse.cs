using UnityEngine;

/// <summary>
/// Sahip olunan mülkün tabanındaki halkayı nabız gibi soldurup parlatır — harita kalabalıkken
/// mülkler bir bakışta bulunsun diye. Renk/hız RealEstateSystem'den gelir.
///
/// Time.unscaledTime kullanır: oyun duraklatıldığında da (menü/skill ağacı açıkken) işaret
/// canlı kalır.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PropertyMarkerPulse : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.cyan;
    private float speed     = 2f;
    private float minAlpha  = 0.30f;
    private float maxAlpha  = 0.95f;

    public void Configure(Color color, float pulseSpeed, float alphaMin, float alphaMax)
    {
        baseColor = color;
        speed     = pulseSpeed;
        minAlpha  = alphaMin;
        maxAlpha  = alphaMax;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        Apply(maxAlpha);
    }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (spriteRenderer == null) return;

        //hız 0 → sabit parlaklık (nabız istenmiyorsa)
        if (speed <= 0f) { Apply(maxAlpha); return; }

        float wave = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        Apply(Mathf.Lerp(minAlpha, maxAlpha, wave));
    }

    private void Apply(float alpha)
    {
        spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }
}
