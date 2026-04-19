using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    // ------------------------------------------------------------------
    // TIME SETTINGS
    // ------------------------------------------------------------------

    [Header("Cycle Durations (seconds)")]
    [Tooltip("How long full daylight lasts.")]
    public float dayLength = 60f;

    [Tooltip("How long full night lasts.")]
    public float nightLength = 40f;

    [Tooltip("Transition time from day to night.")]
    public float duskLength = 15f;

    [Tooltip("Transition time from night to day.")]
    public float dawnLength = 15f;

    // ------------------------------------------------------------------
    // LIGHTING COLORS
    // ------------------------------------------------------------------

    [Header("Lighting Colors")]
    [Tooltip("Tint during full daylight.")]
    public Color dayColor = Color.white;

    [Tooltip("Tint during dusk transition midpoint.")]
    public Color duskColor = new Color(1f, 0.65f, 0.4f, 1f);

    [Tooltip("Tint during full night.")]
    public Color nightColor = new Color(0.15f, 0.15f, 0.35f, 1f);

    [Tooltip("Tint during dawn transition midpoint.")]
    public Color dawnColor = new Color(0.9f, 0.55f, 0.45f, 1f);

    // ------------------------------------------------------------------
    // REFERENCES
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The map's SpriteRenderer. Tint is applied to its color.")]
    public SpriteRenderer mapRenderer;

    // ------------------------------------------------------------------
    // STATE
    // ------------------------------------------------------------------

    public enum Phase { Day, Dusk, Night, Dawn }

    /// <summary>Current phase of the cycle.</summary>
    public Phase CurrentPhase { get; private set; } = Phase.Day;

    /// <summary>Current computed tint — other systems can sample this.</summary>
    public Color CurrentTint { get; private set; } = Color.white;

    /// <summary>Normalized time-of-day (0 = start of day, 1 = full cycle complete).</summary>
    public float NormalizedTime { get; private set; }

    /// <summary>
    /// 0 = full daylight, 1 = full night.
    /// Ramps smoothly during dusk (0→1) and dawn (1→0).
    /// Use this to crossfade day/night sprites, light intensities, etc.
    /// </summary>
    public float LightingRatio { get; private set; }

    [Header("Runtime")]
    [Tooltip("Pause the cycle.")]
    public bool paused = false;

    private float cycleTimer;
    private float totalCycleLength;

    private float dayEnd;
    private float duskEnd;
    private float nightEnd;
    private float dawnEnd;

    // ------------------------------------------------------------------
    // LIFECYCLE
    // ------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()  { RecalculateBoundaries(); }

    void OnValidate()
    {
        dayLength   = Mathf.Max(0.1f, dayLength);
        nightLength = Mathf.Max(0.1f, nightLength);
        duskLength  = Mathf.Max(0.1f, duskLength);
        dawnLength  = Mathf.Max(0.1f, dawnLength);
        RecalculateBoundaries();
    }

    void Update()
    {
        if (paused) return;

        cycleTimer += Time.deltaTime;
        if (cycleTimer >= totalCycleLength)
            cycleTimer -= totalCycleLength;

        NormalizedTime = cycleTimer / totalCycleLength;
        CurrentTint    = EvaluateTint(cycleTimer);
        LightingRatio  = EvaluateLightingRatio(cycleTimer);

        ApplyMapTint(CurrentTint);
    }

    // ------------------------------------------------------------------
    // CORE
    // ------------------------------------------------------------------

    void RecalculateBoundaries()
    {
        dayEnd           = dayLength;
        duskEnd          = dayEnd   + duskLength;
        nightEnd         = duskEnd  + nightLength;
        dawnEnd          = nightEnd + dawnLength;
        totalCycleLength = dawnEnd;
    }

    Color EvaluateTint(float t)
    {
        if (t < dayEnd)
        {
            CurrentPhase = Phase.Day;
            return dayColor;
        }
        if (t < duskEnd)
        {
            CurrentPhase = Phase.Dusk;
            float p = (t - dayEnd) / duskLength;
            return p < 0.5f
                ? Color.Lerp(dayColor, duskColor, p * 2f)
                : Color.Lerp(duskColor, nightColor, (p - 0.5f) * 2f);
        }
        if (t < nightEnd)
        {
            CurrentPhase = Phase.Night;
            return nightColor;
        }
        {
            CurrentPhase = Phase.Dawn;
            float p = (t - nightEnd) / dawnLength;
            return p < 0.5f
                ? Color.Lerp(nightColor, dawnColor, p * 2f)
                : Color.Lerp(dawnColor, dayColor, (p - 0.5f) * 2f);
        }
    }

    float EvaluateLightingRatio(float t)
    {
        if (t < dayEnd)   return 0f;
        if (t < duskEnd)  return Mathf.SmoothStep(0f, 1f, (t - dayEnd) / duskLength);
        if (t < nightEnd) return 1f;
        return Mathf.SmoothStep(1f, 0f, (t - nightEnd) / dawnLength);
    }

    void ApplyMapTint(Color tint)
    {
        if (mapRenderer != null)
            mapRenderer.color = tint;
    }

    // ------------------------------------------------------------------
    // PUBLIC HELPERS
    // ------------------------------------------------------------------

    public void SetTime(float normalized01)
    {
        RecalculateBoundaries();
        cycleTimer     = Mathf.Clamp01(normalized01) * totalCycleLength;
        NormalizedTime = normalized01;
        CurrentTint    = EvaluateTint(cycleTimer);
        LightingRatio  = EvaluateLightingRatio(cycleTimer);
        ApplyMapTint(CurrentTint);
    }

    public void JumpToPhase(Phase phase)
    {
        RecalculateBoundaries();
        switch (phase)
        {
            case Phase.Day:   cycleTimer = 0f;       break;
            case Phase.Dusk:  cycleTimer = dayEnd;    break;
            case Phase.Night: cycleTimer = duskEnd;   break;
            case Phase.Dawn:  cycleTimer = nightEnd;  break;
        }
        CurrentTint   = EvaluateTint(cycleTimer);
        LightingRatio = EvaluateLightingRatio(cycleTimer);
        ApplyMapTint(CurrentTint);
    }

    public bool IsDark() => LightingRatio > 0.7f;

    /// <summary>
    /// 0 = şafak (güneş doğuda, gölge batıya uzun)
    /// 0.5 = öğle (güneş tepede, gölge kısa, arkada)
    /// 1 = akşam (güneş batıda, gölge doğuya uzun)
    /// -1 = gece
    /// </summary>
    public float SunProgress
    {
        get
        {
            if (CurrentPhase == Phase.Night) return -1f;

            float lightPeriod = dawnLength + dayLength + duskLength;
            float pos;

            if (CurrentPhase == Phase.Dawn)
                pos = cycleTimer - nightEnd;          // 0 → dawnLength
            else if (CurrentPhase == Phase.Day)
                pos = dawnLength + cycleTimer;        // dawnLength → dawnLength+dayLength
            else // Dusk
                pos = dawnLength + dayLength + (cycleTimer - dayEnd);

            return Mathf.Clamp01(pos / lightPeriod);
        }
    }
}