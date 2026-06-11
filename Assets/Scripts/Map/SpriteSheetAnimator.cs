using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float frameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int currentFrame;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
