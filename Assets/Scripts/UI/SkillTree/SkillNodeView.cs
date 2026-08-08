using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ağaçtaki tek bir skill node'u. Görselleri runtime'da kurulur (prefab gerekmez),
/// durum değişince sadece renk/alfa güncellenir.
/// </summary>
public class SkillNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public Skill Skill { get; private set; }
    public SkillNodeState State { get; private set; } = SkillNodeState.Locked;
    public RectTransform Rect { get; private set; }

    private SkillTreeView tree;
    private SkillTreeStyle style;
    private SkillBranch branch;

    private Image fill;
    private Image ring;
    private Image icon;
    private Image accent;
    private TextMeshProUGUI label;
    private TextMeshProUGUI cost;

    private float targetScale = 1f;
    private bool hovered;

    private bool hasActiveAbility;
    private Image activeTrack; //bekleme süresinin arka halkası
    private Image activeFill;  //dolan kısım
    private Image lockBadge;
    private Image lockBacking;

    private Image holdFill; //basılı tutarken dolan satın alma halkası
    private bool holding;
    private float holdTimer;
    private Coroutine rewindRoutine;

    private Coroutine deniedRoutine;
    private Coroutine punchRoutine;
    private bool punching; //Update'in ölçek lerp'i animasyonla çakışmasın

    /// <summary>
    /// Basılı tutarak satın alma tamamlandıktan sonra parmak kalkınca OnPointerClick de
    /// tetiklenir. O anda node artık "alınmış + aktif yeteneği var" durumundadır, yani
    /// tıklama yeteneği ANINDA kullanır — oyuncu sadece satın almak istemişken harita
    /// modu açılıyordu. Satın almayı izleyen ilk tıklama bu bayrakla yutulur.
    /// </summary>
    private bool suppressClickAfterPurchase;

    /// <summary>
    /// Coroutine ile doğan geçici görseller (patlama halkası, süpürme yayı). Normalde
    /// rutinin sonunda yok edilirler — AMA GameObject devre dışı kalınca coroutine'ler
    /// öldüğü için o satıra hiç ulaşılmaz ve görsel yarı animasyonlu halde ekranda kalır.
    /// Bölge çizim modu ağacı kapattığı için bu HER SEFERİNDE oluyordu.
    /// </summary>
    private readonly System.Collections.Generic.List<GameObject> transientVisuals
        = new System.Collections.Generic.List<GameObject>();

    private void OnDisable()
    {
        //panel kapanırken yarım kalan her şeyi temizle
        for (int i = 0; i < transientVisuals.Count; i++)
            if (transientVisuals[i] != null) Destroy(transientVisuals[i]);
        transientVisuals.Clear();

        //rutinler ölmüş olacak; bıraktıkları durumu elle geri al
        punching  = false;
        holding   = false;
        holdTimer = 0f;
        rewindRoutine = punchRoutine = deniedRoutine = null;

        if (holdFill != null) { holdFill.fillAmount = 0f; holdFill.enabled = false; }

        targetScale = 1f;
        transform.localScale = Vector3.one;
    }

    public bool HasActiveAbility => hasActiveAbility;

    /// <summary>
    /// İsim/fiyat yazılarının genişliği yerleşim moduna göre değişir
    /// (yatayda kolon aralığı, dikeyde şerit aralığı belirler).
    /// </summary>
    public void SetLabelWidth(float width)
    {
        if (label != null) label.rectTransform.sizeDelta = new Vector2(width, label.rectTransform.sizeDelta.y);
        if (cost != null) cost.rectTransform.sizeDelta = new Vector2(width, cost.rectTransform.sizeDelta.y);
    }

    /// <summary>Rozetleri dairenin sağ alt köşesine yerleştirir.</summary>
    private static void SetBadgeRect(RectTransform rect, float size)
    {
        rect.anchorMin = new Vector2(0.82f, 0.18f);
        rect.anchorMax = new Vector2(0.82f, 0.18f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;
    }

    public void Build(SkillTreeView owner, Skill skill, SkillTreeStyle treeStyle, SkillBranch nodeBranch)
    {
        tree = owner;
        Skill = skill;
        style = treeStyle;
        branch = nodeBranch;
        Rect = GetComponent<RectTransform>();

        float size = style.nodeSize;
        Rect.sizeDelta = new Vector2(size, size);

        //arka plan dairesi — tıklama hedefi de bu
        fill = SkillTreeUI.NewImage("Fill", Rect, UISpriteFactory.Circle());
        SkillTreeUI.Stretch(fill.rectTransform);
        fill.raycastTarget = true;

        //ağaç rengini taşıyan ince iç halka — hangi ağaca ait olduğu bir bakışta anlaşılsın
        accent = SkillTreeUI.NewImage("Accent", Rect, UISpriteFactory.Ring(128, 3f));
        SkillTreeUI.Stretch(accent.rectTransform, style.ringThickness + 3f);
        accent.color = style.AccentFor(branch);
        accent.raycastTarget = false;

        //durum halkası
        ring = SkillTreeUI.NewImage("Ring", Rect, UISpriteFactory.Ring(128, style.ringThickness));
        SkillTreeUI.Stretch(ring.rectTransform);
        ring.raycastTarget = false;

        //ikon
        icon = SkillTreeUI.NewImage("Icon", Rect, skill != null ? skill.icon : null);
        SkillTreeUI.Stretch(icon.rectTransform, style.iconPadding);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.enabled = skill != null && skill.icon != null;

        //ikon yoksa skill id'si daire içinde gösterilir — mock ağaçta okunabilirlik için
        if (!icon.enabled)
        {
            TextMeshProUGUI fallback = SkillTreeUI.NewText("Id", Rect, style.labelFontSize + 3f, TextAlignmentOptions.Center);
            SkillTreeUI.Stretch(fallback.rectTransform);
            fallback.text = skill != null ? skill.id.ToUpperInvariant() : "?";
            fallback.fontStyle = FontStyles.Bold;
            fallback.raycastTarget = false;
            icon = null;
            iconFallback = fallback;
        }

        //aktif yeteneği olan skiller: dışarıda ek bir halka.
        //Hazırken parlar, kullanıldıktan sonra bekleme süresince radyal olarak dolar.
        if (SkillTreeManager.HasActiveAbility(skill))
        {
            hasActiveAbility = true;

            activeTrack = SkillTreeUI.NewImage("ActiveTrack", Rect, UISpriteFactory.Ring(128, style.ringThickness));
            SkillTreeUI.Stretch(activeTrack.rectTransform, -style.activeRingOffset);
            activeTrack.raycastTarget = false;

            activeFill = SkillTreeUI.NewImage("ActiveFill", Rect, UISpriteFactory.Ring(128, style.ringThickness));
            SkillTreeUI.Stretch(activeFill.rectTransform, -style.activeRingOffset);
            activeFill.raycastTarget = false;
            activeFill.type = Image.Type.Filled;
            activeFill.fillMethod = Image.FillMethod.Radial360;
            activeFill.fillOrigin = (int)Image.Origin360.Top;
            activeFill.fillClockwise = true;
            activeFill.fillAmount = 1f;
        }

        //satın alma halkası: basılı tutarken dolar. Aktif yetenek halkasıyla aynı yerde durur
        //ama ikisi asla aynı anda görünmez (biri alınmamış, diğeri alınmış node'da çalışır).
        holdFill = SkillTreeUI.NewImage("HoldFill", Rect, UISpriteFactory.Ring(128, style.ringThickness + 2f));
        SkillTreeUI.Stretch(holdFill.rectTransform, -style.activeRingOffset);
        holdFill.raycastTarget = false;
        holdFill.type = Image.Type.Filled;
        holdFill.fillMethod = Image.FillMethod.Radial360;
        holdFill.fillOrigin = (int)Image.Origin360.Top;
        holdFill.fillClockwise = true;
        holdFill.fillAmount = 0f;
        holdFill.enabled = false;

        //kilit rozeti — sağ altta, koyu bir daire üstünde ki ikon eklenince de okunur kalsın
        lockBacking = SkillTreeUI.NewImage("LockBg", Rect, UISpriteFactory.Circle());
        SetBadgeRect(lockBacking.rectTransform, size * 0.46f);
        lockBacking.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);

        lockBadge = SkillTreeUI.NewImage("Lock", Rect, UISpriteFactory.Lock());
        SetBadgeRect(lockBadge.rectTransform, size * 0.30f);

        //isim — dairenin altında
        label = SkillTreeUI.NewText("Label", Rect, style.labelFontSize, TextAlignmentOptions.Top);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0.5f, 0f);
        lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(style.columnSpacing - 40f, 44f);
        lr.anchoredPosition = new Vector2(0f, -style.labelOffset);
        label.text = skill != null ? DisplayName(skill) : "?";
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.Normal;

        //fiyat — ismin altında
        cost = SkillTreeUI.NewText("Cost", Rect, style.costFontSize, TextAlignmentOptions.Top);
        RectTransform cr = cost.rectTransform;
        cr.anchorMin = new Vector2(0.5f, 0f);
        cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(style.columnSpacing - 40f, 26f);
        cr.anchoredPosition = new Vector2(0f, -style.labelOffset - 42f);
        cost.raycastTarget = false;
        cost.fontStyle = FontStyles.Bold;
    }

    private TextMeshProUGUI iconFallback;

    private static string DisplayName(Skill skill)
    {
        return string.IsNullOrWhiteSpace(skill.displayName) ? skill.id.ToUpperInvariant() : skill.displayName;
    }

    public void SetState(SkillNodeState newState)
    {
        State = newState;
        SkillTreeStyle.StateColors c = style.For(newState);

        fill.color = c.fill;
        ring.color = c.ring;
        label.color = c.text;

        //ağaç rengi kilitliyken sönük, açılınca canlı
        Color a = style.AccentFor(branch);
        a.a = newState == SkillNodeState.Locked || newState == SkillNodeState.Blocked ? 0.25f : 0.9f;
        accent.color = a;

        if (icon != null)
        {
            //kilitli node'un ikonu koyulaşır ama silueti okunur kalır
            float v = newState == SkillNodeState.Unlocked ? 1f
                : newState == SkillNodeState.Available ? 0.92f
                : newState == SkillNodeState.Blocked ? 0.35f : 0.55f;
            icon.color = new Color(v, v, v, 1f);
        }

        if (iconFallback != null)
            iconFallback.color = c.text;

        //kilit rozeti sadece gerçekten açılamayan node'larda — parası yetmeyende değil,
        //orada eksik olan ön koşul değil, para
        bool showLock = newState == SkillNodeState.Locked || newState == SkillNodeState.Blocked;
        lockBadge.enabled = showLock;
        lockBacking.enabled = showLock;
        if (showLock)
            lockBadge.color = c.ring;

        if (Skill == null)
        {
            cost.text = string.Empty;
            return;
        }

        switch (newState)
        {
            case SkillNodeState.Unlocked:
                cost.text = "ALINDI";
                cost.color = c.ring;
                break;
            case SkillNodeState.Blocked:
                cost.text = "KİLİTLİ";
                cost.color = c.ring;
                break;
            default:
                cost.text = Skill.cost > 0 ? SkillTreeUI.FormatMoney(Skill.cost) : "BEDAVA";
                cost.color = newState == SkillNodeState.Unaffordable ? style.unaffordable.ring : c.text;
                break;
        }
    }

    /// <summary>
    /// Aktif yeteneğin görselini günceller: hazırken nabız gibi parlar,
    /// bekleme sırasında node sönükleşir ve halka dolarak kalan süreyi gösterir.
    /// </summary>
    private void UpdateActiveAbility()
    {
        if (!hasActiveAbility || activeFill == null) return;

        //henüz alınmamışsa aktif göstergeler kapalı
        bool owned = State == SkillNodeState.Unlocked;
        activeTrack.enabled = owned;
        activeFill.enabled = owned;
        if (!owned) return;

        SkillTreeManager manager = SkillTreeManager.Instance;
        float progress = manager != null ? manager.GetCooldownProgress(Skill) : 1f;
        bool ready = progress >= 1f;

        activeFill.fillAmount = progress;

        Color track = style.activeCooldownColor;
        track.a = 0.18f;
        activeTrack.color = track;

        //Bekleme süresi OLMAYAN yetenekler (ör. bir harita aracını açanlar):
        //halka DURUR ama SÖNÜK ve HAREKETSİZ çizilir. Amaç, "bu skill kullanılabilir"
        //işaretinin b8 gibi bekleme süresi olan skillerle aynı dili konuşması; nabız ve
        //tam parlaklık ise gerçekten bir sayaç dönerken saklanır.
        if (Skill.activeAbility.cooldownSeconds <= 0f)
        {
            activeTrack.enabled = true;
            activeFill.enabled  = true;

            Color idleTrack = style.activeReadyColor;
            idleTrack.a = 0.12f;
            activeTrack.color = idleTrack;

            Color idleRing = style.activeReadyColor;
            idleRing.a = 0.45f;
            activeFill.color = idleRing;

            fill.color = style.unlocked.fill;
            cost.text  = "KULLAN";
            cost.color = style.activeReadyColor;
            return;
        }

        if (ready)
        {
            Color highlight = style.activeReadyColor;

            if (style.activeReadyPulseSpeed > 0f)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * style.activeReadyPulseSpeed) + 1f) * 0.5f;
                highlight.a = Mathf.Lerp(0.55f, 1f, wave);
            }

            activeFill.color = highlight;
            fill.color = Color.Lerp(style.unlocked.fill, style.activeReadyColor * 0.35f, 0.5f);
            cost.text = "HAZIR";
            cost.color = style.activeReadyColor;
            return;
        }

        //bekleme: tükenmiş görünüm
        activeFill.color = style.activeCooldownColor;
        fill.color = style.unlocked.fill * 0.55f;

        float remaining = manager != null ? manager.GetCooldownRemaining(Skill.id) : 0f;
        cost.text = remaining >= 60f
            ? $"{Mathf.CeilToInt(remaining / 60f)} dk"
            : $"{Mathf.CeilToInt(remaining)} sn";
        cost.color = style.activeCooldownColor;
    }

    private void Update()
    {
        UpdateActiveAbility();
        UpdateHold();

        //punch animasyonu ölçeği kendisi sürüyor
        if (punching) return;

        float current = transform.localScale.x;
        if (Mathf.Abs(current - targetScale) < 0.001f)
        {
            //hedefe ulaşıldı — 82 node'un her karede iş yapmasına gerek yok
            if (current != targetScale)
                transform.localScale = new Vector3(targetScale, targetScale, 1f);
            return;
        }

        float t = 1f - Mathf.Exp(-style.hoverLerpSpeed * Time.unscaledDeltaTime);
        current = Mathf.Lerp(current, targetScale, t);
        transform.localScale = new Vector3(current, current, 1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        //sürükleme sonrası bırakma tıklama sayılmasın (pan yaparken skill tetiklenmesin)
        if (eventData.dragging) return;
        if (Skill == null) return;

        //az önce satın alındıysa bu tıklama satın almanın devamıdır, ayrı bir niyet değil
        if (suppressClickAfterPurchase)
        {
            suppressClickAfterPurchase = false;
            return;
        }

        //satın alma basılı tutmayla olur; tek tık sadece aktif yeteneği kullanır
        if (tree.IsActivatable(this))
            tree.ActivateNode(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Skill == null) return;
        if (tree.IsActivatable(this)) return;

        if (!tree.CanPurchase(this))
        {
            PlayDenied();
            return;
        }

        if (rewindRoutine != null)
        {
            StopCoroutine(rewindRoutine);
            rewindRoutine = null;
        }

        holding = true;
        holdTimer = 0f;
        holdFill.fillAmount = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        CancelHold();
    }

    /// <summary>Basılı tutma iptal — halka anında sıfırlanmaz, yaylanarak geri sarar.</summary>
    private void CancelHold()
    {
        if (!holding) return;

        holding = false;
        holdTimer = 0f;

        if (holdFill == null) return;

        float from = holdFill.fillAmount;
        if (from <= 0.001f)
        {
            holdFill.enabled = false;
            return;
        }

        if (rewindRoutine != null) StopCoroutine(rewindRoutine);
        rewindRoutine = StartCoroutine(RewindRoutine(from));
    }

    /// <summary>
    /// Sönümlü yay: halka sıfıra doğru hızla iner, sıfırda sekip küçülerek durur.
    /// Kosinüs negatife geçtiğinde 0'a kırpılır — sekme hissini bu veriyor.
    /// </summary>
    private System.Collections.IEnumerator RewindRoutine(float from)
    {
        float total = Mathf.Max(0.05f, style.holdRewindDuration);
        float drop = Mathf.Clamp(style.holdRewindDropDuration, 0.02f, total);
        float bounce = total - drop;

        //1) iniş — sekmelerden bağımsız süre, tek başına yavaşlatılabilir
        for (float t = 0f; t < drop; t += Time.unscaledDeltaTime)
        {
            holdFill.fillAmount = from * Mathf.Cos(t / drop * Mathf.PI * 0.5f);
            yield return null;
        }

        holdFill.fillAmount = 0f;

        //2) sekmeler — sıfırdan çıkan, gittikçe alçalan tepeler
        for (float t = 0f; t < bounce; t += Time.unscaledDeltaTime)
        {
            float n = t / bounce;
            float decay = Mathf.Exp(-style.holdRewindDamping * n);
            float wave = Mathf.Abs(Mathf.Sin(n * Mathf.PI * style.holdRewindBounces));
            holdFill.fillAmount = from * style.holdRewindBounceScale * decay * wave;
            yield return null;
        }

        holdFill.fillAmount = 0f;
        holdFill.enabled = false;
        rewindRoutine = null;
    }

    /// <summary>Basılı tutma ilerlemesini işler; dolunca satın alma tetiklenir.</summary>
    private void UpdateHold()
    {
        if (!holding || holdFill == null) return;

        float duration = Mathf.Max(0.01f, style.purchaseHoldSeconds);
        holdTimer += Time.unscaledDeltaTime;

        float progress = Mathf.Clamp01(holdTimer / duration);
        holdFill.enabled = true;
        holdFill.fillAmount = progress;
        holdFill.color = style.purchaseHoldColor;

        if (progress < 1f) return;

        //önce durumu temizle: satın alma Refresh tetikleyeceği için halka açık kalmasın
        holding = false;
        holdTimer = 0f;
        holdFill.enabled = false;

        //parmak kalkınca gelecek tıklama yeteneği kullanmasın — sadece satın alındı
        suppressClickAfterPurchase = true;

        tree.PurchaseNode(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        targetScale = style.hoverScale;
        transform.SetAsLastSibling();
        tree.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        targetScale = 1f;
        //imleç node'dan çıkarsa (pan sırasında da olur) satın alma iptal
        CancelHold();
        tree.HideTooltip(this);
    }

    public bool IsHovered => hovered;

    /// <summary>Skill açıldığında: node büyüyüp yerine oturur, dışarı doğru bir halka dalgası yayılır.</summary>
    public void PlayUnlock()
    {
        SpawnBurst(style.unlocked.ring);
        StartPunch(style.unlockPunchScale, style.unlockPunchDuration);
    }

    /// <summary>Aktif yetenek kullanıldığında: node bir an içeri çöker, sonra toparlanır.</summary>
    public void PlayActivated()
    {
        SpawnBurst(style.activeReadyColor);
        StartPunch(style.activatePunchScale, style.activatePunchDuration);
    }

    private void StartPunch(float peakScale, float duration)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine(peakScale, duration));
    }

    private System.Collections.IEnumerator PunchRoutine(float peakScale, float duration)
    {
        punching = true;
        float from = hovered ? style.hoverScale : 1f;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            //0 -> 1 -> 0 yayı: önce tepe değere git, sonra geri dön
            float phase = Mathf.Sin(t / duration * Mathf.PI);
            float scale = Mathf.Lerp(from, peakScale, phase);
            transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        punching = false;
        punchRoutine = null;
    }

    /// <summary>
    /// Çizgi node'a ulaşınca halkanın etrafını saran süpürme.
    /// Node'un çevresinde döner, tamamlanınca söner.
    /// </summary>
    public void PlayRingSweep(Color color)
    {
        Image sweep = SkillTreeUI.NewImage("Sweep", Rect, UISpriteFactory.Ring(128, style.ringThickness + 2f));
        SkillTreeUI.Stretch(sweep.rectTransform, -style.activeRingOffset);
        sweep.raycastTarget = false;
        sweep.type = Image.Type.Filled;
        sweep.fillMethod = Image.FillMethod.Radial360;
        sweep.fillOrigin = (int)Image.Origin360.Top;
        sweep.fillClockwise = true;
        sweep.fillAmount = 0f;
        sweep.color = color;

        transientVisuals.Add(sweep.gameObject);
        StartCoroutine(RingSweepRoutine(sweep, color));
    }

    private System.Collections.IEnumerator RingSweepRoutine(Image sweep, Color color)
    {
        float duration = Mathf.Max(0.05f, style.ringSweepDuration);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            sweep.fillAmount = Mathf.Clamp01(t / duration);
            yield return null;
        }

        sweep.fillAmount = 1f;

        //halka tamamlandıktan sonra kısa bir sönme
        const float fade = 0.22f;
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            Color c = color;
            c.a = Mathf.Lerp(1f, 0f, t / fade);
            sweep.color = c;
            yield return null;
        }

        if (sweep != null)
        {
            transientVisuals.Remove(sweep.gameObject);
            Destroy(sweep.gameObject);
        }
    }

    /// <summary>Node'dan dışa doğru genişleyip sönen halka.</summary>
    private void SpawnBurst(Color color)
    {
        Image burst = SkillTreeUI.NewImage("Burst", Rect, UISpriteFactory.Ring(128, style.ringThickness * 1.5f));
        SkillTreeUI.Stretch(burst.rectTransform);
        burst.raycastTarget = false;
        burst.color = color;

        transientVisuals.Add(burst.gameObject);
        StartCoroutine(BurstRoutine(burst, color));
    }

    private System.Collections.IEnumerator BurstRoutine(Image burst, Color color)
    {
        float duration = style.burstDuration;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float progress = t / duration;
            float scale = Mathf.Lerp(1f, style.burstMaxScale, progress);
            burst.rectTransform.localScale = new Vector3(scale, scale, 1f);

            Color faded = color;
            faded.a = Mathf.Lerp(0.85f, 0f, progress);
            burst.color = faded;

            yield return null;
        }

        if (burst != null)
        {
            transientVisuals.Remove(burst.gameObject);
            Destroy(burst.gameObject);
        }
    }

    /// <summary>Alınamayan node'a tıklanınca kısa bir uyarı titremesi.</summary>
    public void PlayDenied()
    {
        //sadece kendi rutinini durdurur — patlama/punch animasyonlarını kesmez
        if (deniedRoutine != null) StopCoroutine(deniedRoutine);
        deniedRoutine = StartCoroutine(DeniedRoutine());
    }

    private System.Collections.IEnumerator DeniedRoutine()
    {
        Vector2 origin = Rect.anchoredPosition;
        const float duration = 0.28f;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float damp = 1f - t / duration;
            float offset = Mathf.Sin(t * 70f) * 6f * damp;
            Rect.anchoredPosition = origin + new Vector2(offset, 0f);
            yield return null;
        }

        Rect.anchoredPosition = origin;
    }
}
