using UnityEngine;

/// <summary>
/// Tek bir inşaat dumanı: yükselirken büyür ve söner, ömrü dolunca kendini yok eder.
/// İnşaat süresince tekrar tekrar doğar ki alan boyunca sürekli duman görünsün.
/// </summary>
public class SmokePuff : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 startPosition;
    private float   baseSize;
    private float   riseSpeed;
    private float   lifetime;
    private float   totalDuration; //bu süre boyunca yeniden doğar
    private Color   tint;

    private float age;
    private float elapsed;
    private float drift;

    public void Launch(float size, float rise, float life, float duration, Color color)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        startPosition = transform.position;
        baseSize      = Mathf.Max(0.01f, size);
        riseSpeed     = rise;
        lifetime      = Mathf.Max(0.05f, life);
        totalDuration = Mathf.Max(lifetime, duration);
        tint          = color;

        //aynı anda doğup aynı anda sönmesinler
        age   = Random.Range(0f, lifetime);
        drift = Random.Range(-0.25f, 0.25f);

        Apply();
    }

    private void Update()
    {
        age     += Time.deltaTime;
        elapsed += Time.deltaTime;

        if (age >= lifetime)
        {
            //inşaat sürüyorsa yeniden doğ, bittiyse kaybol
            if (elapsed < totalDuration)
            {
                age   = 0f;
                drift = Random.Range(-0.25f, 0.25f);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Apply();
    }

    private void Apply()
    {
        if (spriteRenderer == null) return;

        float t = Mathf.Clamp01(age / lifetime);

        //yüksel + hafifçe yana savrul
        transform.position = startPosition + new Vector3(drift * t, riseSpeed * t, 0f);

        //büyürken sön
        float scale = baseSize * Mathf.Lerp(0.55f, 1.35f, t);
        transform.localScale = new Vector3(scale, scale, 1f);

        //baştan hızlı görünür, sonra yavaşça kaybolur
        float alpha = Mathf.Sin(t * Mathf.PI) * tint.a;
        spriteRenderer.color = new Color(tint.r, tint.g, tint.b, alpha);
    }
}
