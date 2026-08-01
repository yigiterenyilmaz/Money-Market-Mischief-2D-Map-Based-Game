using System;
using UnityEngine;

/// <summary>Bir node'un o anki durumu — görsel tamamen buna göre belirlenir.</summary>
public enum SkillNodeState
{
    Locked,       //ön koşullar sağlanmadı
    Unaffordable, //ön koşullar tamam, para yetmiyor
    Available,    //şimdi alınabilir
    Unlocked,     //alındı
    Blocked,      //başka bir skill yüzünden kalıcı kilitli
}

/// <summary>
/// Skill tree'nin tüm görsel ayarları. Sahnedeki view bunu okur —
/// renk/boyut denemesi için kod değiştirmek gerekmez.
/// </summary>
[CreateAssetMenu(menuName = "SkillTree/Style")]
public class SkillTreeStyle : ScriptableObject
{
    [Header("Arka Plan")]
    [Tooltip("Ağacın arkasındaki sabit, opak zemin — pan/zoom ile kaymaz")]
    public Color background = new Color(0.055f, 0.065f, 0.09f, 1f);
    [Tooltip("Zeminin köşelerine doğru koyulaşması (0 = kapalı)")]
    public float vignetteStrength = 0.35f;
    [Tooltip("Zemindeki ince kareli desen (alfa 0 = kapalı)")]
    public Color backgroundGrid = new Color(0.40f, 0.55f, 0.75f, 0.07f);
    public float gridSize = 64f;
    [Tooltip("Ağaç alanının kenarındaki çerçeve")]
    public Color frameColor = new Color(0.32f, 0.38f, 0.48f, 0.85f);
    public float frameThickness = 3f;

    [Header("Yerleşim")]
    public float columnSpacing = 460f;
    [Tooltip("Node dairesi + altındaki isim ve fiyat kadar yer gerekir; bunun altına inince satırlar çakışır")]
    public float rowSpacing = 300f;
    [Tooltip("İçine ikon konacağı için geniş tutuldu")]
    public float nodeSize = 190f;
    public Vector2 contentPadding = new Vector2(190f, 200f);
    [Tooltip("Ağaç bantlarının içerik dışına taşma miktarı — bant sonsuza uzuyormuş gibi görünsün")]
    public float bandOverdraw = 6000f;

    [Header("Dikey Mod (ağaçlar yan yana)")]
    [Tooltip("Kardeş node'lar arası YATAY mesafe (dikey modda isim genişliğini de belirler)")]
    public float verticalLaneSpacing = 330f;
    [Tooltip("Derinlik seviyeleri arası DİKEY mesafe")]
    public float verticalDepthSpacing = 300f;
    [Tooltip("İki ağaç arasında kaç şeritlik boşluk bırakılsın")]
    public float verticalTreeGap = 1.5f;

    [Header("Ağaç Kısayol Butonları")]
    public bool showBranchButtons = true;
    public Vector2 branchButtonSize = new Vector2(150f, 46f);
    public float branchButtonFontSize = 22f;

    [Header("Node")]
    public float ringThickness = 7f;
    public float iconPadding = 34f;
    public float hoverScale = 1.12f;
    public float hoverLerpSpeed = 14f;

    [Header("Yazı")]
    public float labelFontSize = 24f;
    public float costFontSize = 21f;
    public float labelOffset = 16f;
    public float titleFontSize = 56f;

    [Header("Bağlantı Çizgileri")]
    public float lineThickness = 4f;
    public float lineCornerRadius = 26f;
    [Tooltip("Çizginin node kenarından bıraktığı boşluk")]
    public float lineNodeGap = 6f;

    [Header("Basılı Tutarak Satın Alma")]
    [Tooltip("Skill'in alınması için düğmenin kaç saniye basılı tutulması gerektiği")]
    public float purchaseHoldSeconds = 0.66f;
    [Tooltip("Dolan satın alma halkasının rengi")]
    public Color purchaseHoldColor = new Color(0.45f, 0.98f, 0.72f);
    [Tooltip("Bırakınca halkanın geri sarmasının TOPLAM süresi (iniş + sekmeler)")]
    public float holdRewindDuration = 0.85f;
    [Tooltip("İlk inişin süresi — halkanın dolu halden sıfıra düşmesi. " +
             "Sekmelerden bağımsızdır; büyütmek inişi yavaşlatır.")]
    public float holdRewindDropDuration = 0.3f;
    [Tooltip("Kaç kez sekeceği")]
    public float holdRewindBounces = 2f;
    [Tooltip("İlk sekmenin yüksekliği (dolu halkanın oranı)")]
    public float holdRewindBounceScale = 0.4f;
    [Tooltip("Sekmelerin sönme hızı. Düşük değer = sonraki sekmeler daha belirgin.")]
    public float holdRewindDamping = 1.6f;

    [Header("Animasyonlar")]
    [Tooltip("Skill açılınca çizginin komşu node'a ilerleme süresi")]
    public float edgeDrawDuration = 0.45f;
    [Tooltip("Çizgi ulaşınca node'un etrafını saran halkanın süresi")]
    public float ringSweepDuration = 0.4f;
    [Tooltip("Skill açılınca node'un ulaştığı tepe ölçek")]
    public float unlockPunchScale = 1.35f;
    public float unlockPunchDuration = 0.35f;
    [Tooltip("Aktif yetenek kullanılınca node önce içeri çöker (1'in altı)")]
    public float activatePunchScale = 0.82f;
    public float activatePunchDuration = 0.28f;
    [Tooltip("Yayılan halkanın ulaştığı ölçek")]
    public float burstMaxScale = 2.2f;
    public float burstDuration = 0.5f;

    [Header("Aktif Yetenekler")]
    [Tooltip("Kullanıma hazır aktif skill'in vurgu rengi")]
    public Color activeReadyColor = new Color(1.00f, 0.84f, 0.35f);
    [Tooltip("Bekleme süresini gösteren dolan halkanın rengi")]
    public Color activeCooldownColor = new Color(0.48f, 0.66f, 0.90f);
    [Tooltip("Hazır olduğunda vurgunun nabız hızı (0 = sabit)")]
    public float activeReadyPulseSpeed = 3f;
    [Tooltip("Aktif skill halkasının ana halkadan ne kadar dışarıda olduğu")]
    public float activeRingOffset = 12f;

    [Header("Açılmış Kenar Parlaması")]
    [Tooltip("Parlama katmanının ana çizgiye göre kalınlık çarpanı")]
    public float edgeGlowWidth = 4.5f;
    public float edgeGlowAlpha = 0.3f;
    [Tooltip("Nabız hızı (0 = sabit parlama)")]
    public float edgeGlowPulseSpeed = 1.8f;
    [Tooltip("Nabızda alfanın ne kadar düştüğü (0-1)")]
    public float edgeGlowPulseAmount = 0.45f;

    [Header("Durum Renkleri")]
    //not: bu varsayılanlar sadece YENİ oluşturulan style asset'leri için geçerlidir.
    //var olan SkillTreeStyle.asset kendi serileştirilmiş değerlerini kullanır — orayı düzenle.
    public StateColors locked = new StateColors(
        new Color(0.16f, 0.18f, 0.22f), new Color(0.42f, 0.47f, 0.55f),
        new Color(0.85f, 0.88f, 0.93f), new Color(0.30f, 0.34f, 0.40f));

    public StateColors unaffordable = new StateColors(
        new Color(0.20f, 0.22f, 0.27f), new Color(0.75f, 0.62f, 0.36f),
        new Color(0.97f, 0.91f, 0.74f), new Color(0.42f, 0.38f, 0.31f));

    public StateColors available = new StateColors(
        new Color(0.13f, 0.30f, 0.32f), new Color(0.35f, 0.92f, 0.84f),
        new Color(0.93f, 1.00f, 0.99f), new Color(0.28f, 0.62f, 0.60f));

    public StateColors unlocked = new StateColors(
        new Color(0.12f, 0.38f, 0.26f), new Color(0.40f, 0.96f, 0.60f),
        new Color(0.94f, 1.00f, 0.96f), new Color(0.34f, 0.78f, 0.50f));

    public StateColors blocked = new StateColors(
        new Color(0.24f, 0.12f, 0.14f), new Color(0.80f, 0.34f, 0.38f),
        new Color(0.94f, 0.74f, 0.76f), new Color(0.45f, 0.22f, 0.25f));

    [Header("Ağaç Renkleri (başlık ve vurgu)")]
    public Color financeAccent = new Color(0.98f, 0.83f, 0.42f);
    public Color politicsAccent = new Color(0.62f, 0.78f, 1.00f);
    public Color mediaAccent = new Color(0.90f, 0.66f, 1.00f);
    public Color hybridAccent = new Color(1.00f, 0.62f, 0.50f);

    [Header("Tooltip")]
    public Color tooltipBackground = new Color(0.07f, 0.08f, 0.11f, 0.98f);
    public Color tooltipBorder = new Color(0.35f, 0.40f, 0.48f, 1f);
    public Color tooltipText = new Color(0.95f, 0.97f, 1.00f);
    public Color tooltipMuted = new Color(0.80f, 0.84f, 0.90f);
    public Color tooltipWarning = new Color(0.95f, 0.55f, 0.45f);
    public float tooltipWidth = 740f;
    public float tooltipTitleFontSize = 40f;
    public float tooltipBranchFontSize = 25f;
    public float tooltipBodyFontSize = 28f;

    [Serializable]
    public class StateColors
    {
        public Color fill;
        public Color ring;
        public Color text;
        public Color line;

        public StateColors(Color fill, Color ring, Color text, Color line)
        {
            this.fill = fill;
            this.ring = ring;
            this.text = text;
            this.line = line;
        }
    }

    public StateColors For(SkillNodeState state)
    {
        switch (state)
        {
            case SkillNodeState.Unlocked: return unlocked;
            case SkillNodeState.Available: return available;
            case SkillNodeState.Unaffordable: return unaffordable;
            case SkillNodeState.Blocked: return blocked;
            default: return locked;
        }
    }

    /// <summary>
    /// Ağaç rengini koyu zeminde okunabilir hale getirir.
    /// Ham accent (özellikle mor/mavi) yazı olarak kullanıldığında zeminle karışıyor.
    /// </summary>
    public Color ReadableAccent(SkillBranch branch)
    {
        return Color.Lerp(AccentFor(branch), Color.white, 0.5f);
    }

    public Color AccentFor(SkillBranch branch)
    {
        //birden fazla ağaca aitse hybrid rengi
        bool multi = branch != SkillBranch.None && (branch & (branch - 1)) != 0;
        if (multi) return hybridAccent;

        switch (branch)
        {
            case SkillBranch.Finance: return financeAccent;
            case SkillBranch.Politics: return politicsAccent;
            case SkillBranch.Media: return mediaAccent;
            default: return Color.white;
        }
    }
}
