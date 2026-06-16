using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float frameRate = 12f;

    [Tooltip("Açıksa her instance rastgele bir faz kaymasıyla başlar → aynı sprite'lar senkron oynamaz. " +
             "startPhase01 >= 0 verilirse o açık faz kullanılır (rastgelelik devre dışı).")]
    public bool randomizeStartPhase = true;

    [Tooltip("Açık başlangıç fazı [0,1). >= 0 ise rastgele yerine bu kullanılır. Aynı binanın gündüz/gece " +
             "animatörlerine AYNI değeri vererek ikisini senkron tutmak için. < 0 = kullanılmaz.")]
    public float startPhase01 = -1f;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int currentFrame;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // frames/frameRate AddComponent'tan SONRA atandığı için (Awake çok erken) faz kaymasını
    // Start'ta veriyoruz: farklı binalar senkron oynamasın, ama aynı binanın gündüz/gece animatörleri
    // ortak startPhase01 ile aynı frame'de kalsın.
    private void Start()
    {
        if (frames == null || frames.Length < 2) return;

        // Açık faz verildiyse onu kullan; yoksa randomizeStartPhase açıksa rastgele üret.
        float phase;
        if (startPhase01 >= 0f)      phase = Mathf.Repeat(startPhase01, 1f);
        else if (randomizeStartPhase) phase = Random.value;
        else return;

        float pos    = phase * frames.Length;     // [0, frames.Length)
        currentFrame = Mathf.Clamp((int)pos, 0, frames.Length - 1);
        if (frameRate > 0f) timer = (pos - currentFrame) * (1f / frameRate);
        spriteRenderer.sprite = frames[currentFrame];
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2 || frameRate <= 0f) return;

        timer += Time.deltaTime;
        float interval = 1f / frameRate;
        if (timer >= interval)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
