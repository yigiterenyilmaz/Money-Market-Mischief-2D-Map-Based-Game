# Sosyal Medya Sistemi

## Genel Bakış

Oyunda sürekli akan bir sosyal medya feed'i var. Bu feed oyuncudan bağımsız olarak kendi kendine çalışır - yorumlar düşer, konular değişir, trendler oluşur. Oyuncu bu akışı izler ve bazı yeteneklerle müdahale edebilir.

---

## Sistem Akış Şeması

```
┌─────────────────────────────────────────────────────────────────┐
│                         OYUN BAŞLAR                              │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                  HASSAS KONU BELİRLENİR                          │
│     (ülkenin hassas konusu random seçilir, oyun boyunca sabit)  │
│                                                                  │
│         → SocialMediaManager.DetermineSensitiveTopic()          │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    DOĞAL TREND BELİRLENİR                        │
│         (rastgele bir konu seçilir, 40-150 sn sürer)            │
│                                                                  │
│         → SocialMediaManager.SelectNewNaturalTrend()            │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      YORUM AKIŞI BAŞLAR                          │
│              (her 1-6 saniyede bir yorum düşer)                  │
│                                                                  │
│         → SocialMediaManager.TryShowNewPost()                   │
└─────────────────────────────────────────────────────────────────┘
                                │
                ┌───────────────┼───────────────┐
                ▼               ▼               ▼
        ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
        │  %28-45'i    │ │  Geri kalan  │ │   Override   │
        │ trend konu   │ │   rastgele   │ │    varsa     │
        │   hakkında   │ │    konular   │ │   %70-90     │
        └──────────────┘ └──────────────┘ └──────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SÜRE DOLUNCA                                │
│            (trend değişir, döngü tekrarlar)                      │
└─────────────────────────────────────────────────────────────────┘
```

---

## Oyuncu Müdahalesi Şeması

```
                        OYUNCU
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
    ┌─────────┐      ┌─────────┐      ┌─────────┐
    │  KONU   │      │   HIZ   │      │ KONTROL │
    │ DEĞİŞTİR│      │ ARTIR   │      │ (Freeze/│
    │         │      │         │      │  Slow)  │
    └────┬────┘      └────┬────┘      └────┬────┘
         │                │                │
         │                │                │
         ▼                ▼                ▼
  SetPlayerOverride() SetSpeedBoost()  TryFreezeFeed()
  SetEventOverride()                   TrySlowFeed()
         │                │                │
         ▼                ▼                ▼
    Feed'in %70-90'ı   Yorumlar daha    Feed durur
    seçilen konu       hızlı gelir      veya yavaşlar
    hakkında olur      (min 1 sn)
         │                │                │
         ▼                ▼                ▼
    ┌─────────────────────────────────────────┐
    │        SÜRE DOLUNCA NORMAL AKIŞA        │
    │               GERİ DÖNÜLÜR              │
    └─────────────────────────────────────────┘
```

---

## Detaylı Açıklama

### Feed Nedir?

Feed, oyun ekranında sürekli akan bir yorum akışıdır. Her yorum bir "konu" (topic) hakkındadır: Tax, Politics, Scandal, StockMarket, Technology, RealEstate, Banking, Celebrity, Sports, General, Militarism, WarJustification, AntiWarSpeech. Yorumlar belirli aralıklarla otomatik olarak düşer ve oyuncu bunları okuyarak oyundaki durumu anlar.

Konular `TopicType` enum'ında tanımlı, yorumlar `SocialMediaPost` olarak tutulur.

### Trend Sistemi

Sistem her 40 ila 150 saniyede bir "trend konu" belirler. Bu süre boyunca feed'e düşen yorumların yaklaşık %28-45'i bu trend konu hakkında olur. Kalan yorumlar diğer konulardan rastgele gelir. Süre dolduğunda sistem yeni bir trend seçer ve döngü devam eder.

Trend seçimi `SelectNewNaturalTrend()` ile yapılır. Hangi konunun trend olacağı ağırlıklı rastgele seçimle belirlenir - skill'ler `ModifyTopicWeight()` ile bu ağırlıkları değiştirebilir.

### Oyuncu Ne Yapabilir?

Oyuncu belirli skill'leri açarak feed üzerinde kontrol kazanır:

**1. Konu Değiştirme**
Oyuncu istediği bir konuyu öne çıkarabilir. Bu durumda feed'in %70-90'ı o konu hakkında olur.
- Skill ile: `SetPlayerOverride()` çağrılır
- Event ile: `SetEventOverride()` çağrılır (custom oran ve süre)

**2. Hız Artırma**
Yorumların daha sık gelmesini sağlar. Normalde 1-6 saniye olan aralık kısalır.
- Skill ile: `SetSpeedBoost()` çağrılır
- Event ile: `SetSpeedBoostWithDuration()` çağrılır

**3. Dondurma/Yavaşlatma**
Önce yetenek açılmalı, sonra oyuncu istediği zaman kullanabilir.
- Yetenek açma: `UnlockFreezeAbility()` veya `UnlockSlowAbility()`
- Kullanma: `TryFreezeFeed()` veya `TrySlowFeed()`

### Event'ler Ne Yapar?

`RandomEventManager.SelectChoice()` bir event seçeneği işlendiğinde, eğer o seçenekte feed ayarları varsa ilgili metodları çağırır.

### Hassas Konu (Sensitive Topic)

Her oyun başında ülkenin "hassas konusu" rastgele belirlenir. Bu konu oyun boyunca değişmez. Hassas konunun doğal trend olma şansı diğer konularla aynıdır, ancak ileride bu konuda yapılan manipülasyonlar (override, bot basma vb.) daha güçlü etki yaratacak.

- Belirleme: `DetermineSensitiveTopic()` - Start'ta çağrılır
- Sorgulama: `GetSensitiveTopic()` - hassas konuyu döndürür
- Kontrol: `IsSensitiveTopic(topic)` - verilen konu hassas mı

---

## Kim Neyi Yapar?

| İş | Sorumlu |
|----|---------|
| Yorum verisi tutma | `SocialMediaPost` |
| Tüm yorumları saklama | `SocialMediaPostDatabase` |
| Trend belirleme | `SocialMediaManager.SelectNewNaturalTrend()` |
| Yorum düşürme | `SocialMediaManager.TryShowNewPost()` |
| Yorum seçme | `SocialMediaManager.GetNextPost()` |
| Topic override (skill) | `SocialMediaManager.SetPlayerOverride()` |
| Topic override (event) | `SocialMediaManager.SetEventOverride()` |
| Hız artırma | `SocialMediaManager.SetSpeedBoost()` |
| Freeze/Slow yeteneği açma | `UnlockFreezeAbility()` / `UnlockSlowAbility()` |
| Freeze/Slow kullanma | `TryFreezeFeed()` / `TrySlowFeed()` |
| Topic ağırlığı değiştirme | `SocialMediaManager.ModifyTopicWeight()` |
| Hassas konu belirleme | `SocialMediaManager.DetermineSensitiveTopic()` |
| Hassas konu sorgulama | `GetSensitiveTopic()` / `IsSensitiveTopic()` |

---

## Özet Tablo

| Durum | Yorum Aralığı | Trend Oranı | Süre |
|-------|---------------|-------------|------|
| Normal | 1-6 sn | %28-45 | 40-150 sn |
| Override | 1-6 sn | %70-90 | Değişken |
| Hız Boost | min 1 sn | - | Değişken |
| Yavaşlatılmış | 8-15 sn | - | Sabit |
| Dondurulmuş | ∞ (durur) | - | Sabit |

---

## Event'ler (Bildirimler)

Sistem önemli anlarda event fırlatır, UI bu event'lere abone olarak güncellenir:

| Event | Ne Zaman |
|-------|----------|
| `OnNaturalTrendChanged` | Doğal trend değiştiğinde |
| `OnOverrideStarted` | Override başladığında |
| `OnOverrideEnded` | Override bittiğinde |
| `OnNewPost` | Yeni yorum düştüğünde |
| `OnSpeedBoostStarted` | Hız boost başladığında |
| `OnSpeedBoostEnded` | Hız boost bittiğinde |
| `OnFeedFrozen` | Feed dondurulduğunda |
| `OnFeedUnfrozen` | Dondurma bittiğinde |
| `OnFeedSlowed` | Feed yavaşlatıldığında |
| `OnFeedSpeedRestored` | Yavaşlatma bittiğinde |
| `OnFreezeAbilityUnlocked` | Dondurma yeteneği açıldığında |
| `OnSlowAbilityUnlocked` | Yavaşlatma yeteneği açıldığında |
| `OnSensitiveTopicDetermined` | Oyun başında hassas konu belirlendiğinde |
