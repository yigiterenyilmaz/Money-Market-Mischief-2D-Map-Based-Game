# IllegalScientistProvider Minigame

Ülkelere kaçak nükleer bilim adamı sağlama minigame'i. Oyuncuya belirli aralıklarla ülke teklifleri gelir, oyuncu eğitimli bilim adamlarından birini göndererek operasyonu başlatır. Operasyon süresince risk tabanlı rastgele deşifre olma tehlikesi vardır. Operasyon sonrası musallat eventleri oyuncuyu rahatsız eder.

---

## Dosya Yapısı

```
Assets/
├── Scripts/Minigames/IllegalScientistProvider/
│   ├── IllegalScientistProviderEvent.cs       ← Event ScriptableObject (teklif, süreç, musallat)
│   ├── IllegalScientistProviderDatabase.cs    ← Event havuzlarını tutan veritabanı
│   └── IllegalScientistProviderManager.cs     ← Ana yönetici (MonoBehaviour Singleton)
├── Editor/
│   └── IllegalScientistProviderEventEditor.cs ← Event Inspector'ı (tipe göre alan gösterimi)
```

---

## Durum Makinesi

```
Idle ──→ OfferPending ──→ ActiveProcess ←──→ EventPhase
                │                │
                │                ├──→ CompleteProcess ──→ PostProcess ←──→ PostEventPhase ──→ Idle
                │                └──→ FailProcess    ──→ PostProcess ←──→ PostEventPhase ──→ Idle
                │
                └──→ RejectOffer ──→ Idle
```

| Durum | Açıklama |
|-------|----------|
| **Idle** | Teklif bekleniyor. `offerTimer` geri sayıyor. |
| **OfferPending** | Teklif geldi. Oyuncu bilim adamı sürükleyerek kabul eder veya reddeder. Süre dolunca otomatik ret. |
| **ActiveProcess** | Operasyon devam ediyor. Risk kontrolü + event kontrolü yapılıyor. |
| **EventPhase** | Süreç sırasında event geldi. Oyuncu seçim yapana kadar oyun duraklatılır. |
| **PostProcess** | Operasyon bitti (başarılı/başarısız). Musallat eventleri periyodik tetikleniyor. |
| **PostEventPhase** | Musallat event geldi. Oyuncu seçim yapana kadar oyun duraklatılır. |

---

## IllegalScientistProviderEvent (ScriptableObject)

Tek ScriptableObject, `eventType` enum'una göre farklı rollerde kullanılır. Inspector'da Custom Editor sayesinde sadece ilgili alanlar görünür.

### Event Tipleri

**Offer** — Ülke teklifi
| Alan | Tip | Açıklama |
|------|-----|----------|
| `id` | string | Benzersiz kimlik |
| `displayName` | string | Ülke/teklif adı |
| `description` | string (TextArea) | Ülkenin durumu, kriz açıklaması |
| `baseReward` | int | Başarılı operasyon sonrası taban kazanç |
| `riskLevel` | float [0-1] | Ülkenin risk seviyesi (0 = güvenli, 1 = çok riskli) |
| `processEvents` | List\<IllegalScientistProviderEvent\> | Bu offer'a özel süreç eventleri (boşsa database'den alınır) |
| `decisionTime` | float | Oyuncunun karar süresi (saniye) |

**Process** — Süreç sırasında tetiklenen event
| Alan | Tip | Açıklama |
|------|-----|----------|
| `id` | string | Benzersiz kimlik |
| `displayName` | string | Event başlığı |
| `description` | string (TextArea) | Event açıklaması |
| `decisionTime` | float | Karar süresi (saniye) |
| `choices` | List\<IllegalScientistProviderEventChoice\> | Oyuncunun seçenekleri |
| `defaultChoiceIndex` | int | Süre dolunca otomatik seçilecek seçenek (-1 = ilk) |

**PostProcess** — Operasyon sonrası musallat event
| Alan | Tip | Açıklama |
|------|-----|----------|
| `id` | string | Benzersiz kimlik |
| `displayName` | string | Event başlığı |
| `description` | string (TextArea) | Event açıklaması |
| `postProcessEffect` | PostProcessEffectType | Musallat etki tipi |
| ↳ ScientistKill: `scientistKillCount` | int | Öldürülecek bilim adamı sayısı |
| `decisionTime` | float | Karar süresi (saniye) |
| `choices` | List\<IllegalScientistProviderEventChoice\> | Oyuncunun seçenekleri |
| `defaultChoiceIndex` | int | Süre dolunca otomatik seçilecek seçenek (-1 = ilk) |

### IllegalScientistProviderEventChoice

| Alan | Tip | Açıklama |
|------|-----|----------|
| `displayName` | string | Seçenek adı |
| `description` | string (TextArea) | Seçenek açıklaması |
| `riskModifier` | float | Risk değişimi (- = azaltır, + = artırır). Process eventlerinde anlamlı. |
| `suspicionModifier` | float | Şüphe değişimi |
| `costModifier` | int | Ek maliyet/zarar |

### PostProcessEffectType

| Değer | Açıklama |
|-------|----------|
| `None` | Sadece choices etkisi (suspicion/cost) |
| `ScientistKill` | Event tetiklendiğinde rastgele bilim adamı öldürülür |

Yeni musallat etki tipleri eklemek için:
1. `PostProcessEffectType` enum'una yeni değer ekle
2. `IllegalScientistProviderEvent`'e ilgili alanları ekle
3. `IllegalScientistProviderEventEditor`'da yeni tipin alanlarını göster
4. `IllegalScientistProviderManager.ApplyPostProcessEffect()` switch'ine yeni case ekle

---

## IllegalScientistProviderDatabase (ScriptableObject)

Event havuzlarını tutar. Inspector'dan doldurulur.

| Alan | Tip | Açıklama |
|------|-----|----------|
| `offerEvents` | List\<IllegalScientistProviderEvent\> | Teklif eventleri (eventType = Offer) |
| `processEvents` | List\<IllegalScientistProviderEvent\> | Genel süreç eventleri (eventType = Process) |
| `postProcessEvents` | List\<IllegalScientistProviderEvent\> | Musallat eventleri (eventType = PostProcess) |

---

## IllegalScientistProviderManager (MonoBehaviour Singleton)

Ana yönetici. Sahneye eklenir, Inspector'dan ayarlanır.

### Inspector Ayarları

| Alan | Varsayılan | Açıklama |
|------|-----------|----------|
| `minigameData` | — | MinigameManager unlock/cooldown kontrolü için |
| `database` | — | Event veritabanı referansı |
| `minOfferInterval` | 90f | Minimum teklif aralığı (saniye) |
| `maxOfferInterval` | 150f | Maximum teklif aralığı (saniye) |
| `offerDecisionTime` | 15f | Teklif karar süresi (saniye) |
| `processDuration` | 300f | Operasyon süresi (5 dakika) |
| `processEventInterval` | 15f | Süreç sırasında event kontrol aralığı (saniye) |
| `riskCheckInterval` | 1f | Deşifre olma kontrol aralığı (saniye) |
| `riskMultiplier` | 0.003f | Risk çarpanı |
| `postProcessEventInterval` | 20f | Musallat eventleri arası bekleme (saniye) |

### Risk Sistemi

Her `riskCheckInterval` saniyede bir deşifre olma zarı atılır:

```
gameOverChance = riskMultiplier * adjustedRisk * (1 - stealthLevel)
```

- `adjustedRisk` = ülkenin `riskLevel` + süreç eventlerinden biriken `riskModifier` (0-1 arası clamp)
- `stealthLevel` = gönderilen bilim adamının gizlilik seviyesi (0-1)
- Zar tutarsa → operasyon deşifre olur → `FailProcess`

### Teklif Sistemi

- Teklifler `minOfferInterval` - `maxOfferInterval` arası rastgele aralıklarla gelir
- Kabul veya ret edilen teklifler `usedOffers` HashSet'ine eklenir, bir daha gelmez
- Teklif eğitimli bilim adamı olmasa bile gelir (oyuncu zorunlu olarak reddeder)
- Kabul: oyuncu bilim adamını sürükler → bilim adamı listeden kalıcı çıkar → süreç başlar
- Ret: Idle'a dönülür, yeni timer başlar

### Process Eventleri

- Süreç sırasında `processEventInterval` aralıklarla event havuzundan rastgele event seçilir
- Her event sadece bir kez tetiklenir
- Offer'ın kendi `processEvents` listesi varsa o kullanılır, yoksa `database.processEvents`
- Oyuncunun seçimi `riskModifier`, `suspicionModifier`, `costModifier` biriktirir
- Risk/suspicion/cost süreç sonunda topluca uygulanır

### Sonuç Sistemi

**Başarı** (süre doldu):
- `wealthChange = baseReward - accumulatedCostModifier`
- `suspicionChange = accumulatedSuspicionModifier`

**Başarısızlık** (deşifre oldu):
- `wealthChange = -accumulatedCostModifier` (ödül yok)
- `suspicionChange = accumulatedSuspicionModifier`

Her iki durumda da stat'lar hemen uygulanır, ardından PostProcess başlar.

### PostProcess Sistemi (Musallat)

- Operasyon sonrası (başarılı veya başarısız) musallat eventleri başlar
- `database.postProcessEvents` havuzundan periyodik olarak rastgele event tetiklenir
- Her event sadece bir kez tetiklenir
- Event tetiklendiğinde `ApplyPostProcessEffect()` ile etki hemen uygulanır (ör. ScientistKill)
- Oyuncunun seçimi suspicion/cost'u anında uygular (biriktirmez)
- Havuzdaki tüm eventler tetiklenince → `EndPostProcess()` → Idle → yeni teklifler gelebilir
- PostProcess devam ederken yeni operasyon başlatılamaz

---

## UI Event'leri

UI bu static event'leri dinleyerek ekranı günceller:

| Event | Parametre | Ne zaman tetiklenir |
|-------|-----------|---------------------|
| `OnOfferReceived` | `IllegalScientistProviderEvent` | Teklif geldi |
| `OnOfferDecisionTimerUpdate` | `float` | Teklif karar sayacı güncellendi |
| `OnProcessStarted` | `IllegalScientistProviderEvent, float` | Süreç başladı (offer, süre) |
| `OnProcessProgress` | `float` | Süreç ilerlemesi (0-1) |
| `OnSmuggleEventTriggered` | `IllegalScientistProviderEvent` | Event tetiklendi (process veya postProcess) |
| `OnEventDecisionTimerUpdate` | `float` | Event karar sayacı güncellendi |
| `OnSmuggleEventResolved` | `IllegalScientistProviderEventChoice` | Oyuncu seçim yaptı |
| `OnMinigameFailed` | `string` | Operasyon deşifre oldu (sebep) |
| `OnProcessCompleted` | `IllegalScientistProviderResult` | Operasyon başarıyla bitti |
| `OnPostProcessStarted` | — | Musallat süreci başladı |
| `OnPostProcessEnded` | — | Musallat süreci bitti |
| `OnScientistsKilled` | `List<ScientistData>` | Bilim adamları öldürüldü |

### UI'ın Çağıracağı Public Metodlar

| Metod | Açıklama |
|-------|----------|
| `AcceptOffer(int scientistIndex)` | Teklifi kabul et, bilim adamını ata |
| `RejectOffer()` | Teklifi reddet |
| `ResolveEvent(int choiceIndex)` | Process event seçimi yap |
| `ResolvePostEvent(int choiceIndex)` | PostProcess event seçimi yap |

### Getter Metodları

| Metod | Dönüş | Açıklama |
|-------|-------|----------|
| `IsActive()` | bool | Minigame aktif mi (Idle değilse true) |
| `GetCurrentState()` | IllegalScientistProviderState | Mevcut durum |
| `GetProcessProgress()` | float | Süreç ilerlemesi (0-1) |
| `GetEffectiveRisk()` | float | Efektif risk seviyesi (0-1) |

---

## Bağımlılıklar

| Sistem | Kullanım |
|--------|----------|
| `MinigameManager` | `IsMinigameUnlocked()`, `IsOnCooldown()` |
| `GameManager` | `PauseGame()`, `ResumeGame()` |
| `GameStatManager` | `AddWealth()`, `AddSuspicion()` |
| `SkillTreeManager` | `GetScientist()`, `GetScientistCount()`, `RemoveScientist()` |
| `EventCoordinator` | `CanShowEvent()`, `MarkEventShown()` — diğer event sistemleriyle çakışma önleme |

---

## Asset Oluşturma

Unity menüsünden:
- **Assets > Create > Minigames > IllegalScientistProvider > Event** — yeni event oluştur
- **Assets > Create > Minigames > IllegalScientistProvider > Database** — yeni veritabanı oluştur
