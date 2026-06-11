# Please Paper Minigame

Ülkelerin yurt dışında saklanan altınlarını ekonomik krizleri kullanarak ele geçirme minigame'i. Oyuncuya belirli aralıklarla teklif gelir, oyuncu krizin gerçek mi sahte mi olduğunu değerlendirip karar verir. Kendi UI paneli yok — mevcut event panelini kullanır.

---

## Durum Makinesi

```
                          RejectOffer()
                         ┌──────────────── Idle
                         │                  │
Idle ──→ OfferPending ──┤
                         │
                         ├── Sahte kriz → FakeCrisisProcess ←──→ EventPhase
                         │                      │
                         │                      ▼
                         │               CompleteFakeCrisis ──→ Idle (zarar)
                         │
                         └── Gerçek kriz → ActiveProcess ←──→ EventPhase
                                               │
                                               ├── controlStat = 0 → FailProcess ──→ Idle
                                               │
                                               ├── Süre doldu, controlStat < 50 → FailProcess ──→ Idle
                                               │
                                               └── Süre doldu, controlStat >= 50 → BargainingPhase ──→ Idle
```

| Durum | Açıklama |
|-------|----------|
| Idle | Teklif bekleniyor (90-150s arasında rastgele) |
| OfferPending | Teklif geldi, oyuncu kabul/ret bekliyor (15s) |
| FakeCrisisProcess | Sahte kriz — event zinciri sırayla gösterilir, sonuç hep zarar |
| ActiveProcess | Gerçek kriz — 5 dakikalık süreç, controlStat yönetimi |
| EventPhase | Event geldi, oyuncu karar bekliyor (10s) |
| BargainingPhase | Süreç başarılı bitti, kazanç controlStat'a göre hesaplanır |

---

## Akış

```
Skill açılır
    │
    ▼
UnlockMinigameEffect → MinigameManager.UnlockMinigame()
    │
    ▼
Idle — offerTimer geri sayıyor (90-150s)
    │
    ▼
GenerateOffer() → EventCoordinator slot kontrolü
    │
    ├── Slot doluysa → sonraki frame tekrar dener (timer sıfırlanmaz)
    │
    ├── Slot boşsa → completedRealOffers ve lastOffer filtrelenir, havuzdan teklif seçilir
    │     ├── İlk kez gelen offer → isFakeCrisis değeri geçerli
    │     └── revealedFakeOffers'daki offer → realChanceOnRepeat'e göre gerçek/fake belirlenir
    │
    ▼
OfferPending — oyuncuya teklif event'inin bilgisi sunulur (15s)
    │
    ├── RejectOffer() → slot bırakılır, Idle'a dön
    │
    ├── AcceptOffer() → currentOfferIsFake kontrolü (runtime'da belirlenir)
    │       │
    │       ├── Sahte kriz → StartFakeCrisisProcess()
    │       │       │
    │       │       ├── Offer'ın fakeCrisisEvents zinciri sırayla gösterilir
    │       │       │
    │       │       └── Zincir bitti → CompleteFakeCrisis() → zarar uygulanır, revealedFakeOffers'a ekle, Idle
    │       │
    │       └── Gerçek kriz → StartActiveProcess()
    │               │
    │               ├── controlStat = 40, processTimer başlar (300s)
    │               │
    │               ├── Her 15s → TryTriggerProcessEvent()
    │               │       │
    │               │       ├── Event tetiklendi → EventPhase (10s karar süresi)
    │               │       │       │
    │               │       │       ├── controlStatModifier uygulanır
    │               │       │       │
    │               │       │       ├── controlStat = 0 → FailProcess()
    │               │       │       │
    │               │       │       └── Normal → ActiveProcess'e dön
    │               │       │
    │               │       └── Event tetiklenmedi → devam
    │               │
    │               ▼
    │          Süre doldu → CalculateResult()
    │               │
    │               ├── controlStat < 50 → FailProcess()
    │               │
    │               └── controlStat >= 50 → StartBargaining()
    │
    └── Süre doldu → otomatik ret
```

---

## Event Grupları

Tüm eventler tek `PleasePaperEvent` sınıfından oluşturulur. `eventType` alanına göre Inspector'da sadece ilgili alanlar gösterilir.

| Grup | eventType | Açıklama |
|------|-----------|----------|
| Teklif eventleri | Offer | Ülke bilgisi, baseReward, isFakeCrisis taşır. Database'de `offerEvents` listesinde |
| Sahte kriz eventleri | FakeCrisis | Choices ile zarar verir. Offer event'inin `fakeCrisisEvents` listesinde |
| Süreç eventleri | Process | Choices ile controlStat etkiler. Database'de `processEvents` listesinde |

Inspector'da `eventType` seçilince:
- **Offer** → baseReward, isFakeCrisis, realChanceOnRepeat, fakeCrisisEvents, processEvents alanları görünür
- **FakeCrisis / Process** → choices listesi, karar süresi, default seçenek görünür

---

## Asset Oluşturma Rehberi

Tüm asset'ler `Assets/GameData/Minigames/PleasePaper/` klasöründe oluşturulur.
Sağ tık → Create → Minigames → PleasePaper → Event veya Database.

### Yapı

```
PleasePaperDatabase
  ├─ offerEvents (Offer tipinde event'ler)
  │    ├─ Venezuela Krizi (isFakeCrisis = false)
  │    │    └─ processEvents: [Grev, Sızıntı, Pusu]  ← bu offer'a özel süreç eventleri
  │    │
  │    └─ Kıbrıs Tuzağı (isFakeCrisis = true)
  │         ├─ fakeCrisisEvents: [Adım1, Adım2, Adım3]  ← sahte kriz zinciri
  │         └─ processEvents: [Grev2, Pusu2]  ← gerçeğe dönüşürse kullanılacak eventler
  │
  └─ processEvents (Process tipinde yedek event'ler)
       └─ Offer'ın kendi processEvents'i boşsa buradan çekilir
```

### Oluşturma Sırası

1. **Process event'leri** oluştur (eventType = Process, seçenekler + modifier'lar doldur)
2. **FakeCrisis event'leri** oluştur (eventType = FakeCrisis, zincirin her adımı ayrı asset)
3. **Offer event'leri** oluştur (eventType = Offer):
   - Gerçek kriz: isFakeCrisis = false, baseReward doldur, processEvents'e bu offer'a ait süreç eventlerini sürükle
   - Sahte kriz: isFakeCrisis = true, realChanceOnRepeat ayarla, fakeCrisisEvents listesine FakeCrisis event'lerini sürükle, processEvents'e gerçeğe dönüşürse kullanılacak eventleri sürükle
4. **Database** oluştur → offerEvents listesine offer'ları sürükle, processEvents listesine yedek eventleri sürükle (opsiyonel)

FakeCrisis event'leri database'e eklenmez — sahte Offer event'inin içindeki `fakeCrisisEvents` listesine bağlanır.
Process event'leri her Offer'ın kendi `processEvents` listesine bağlanır. Database'deki `processEvents` sadece yedek havuzdur.

---

## controlStat Mekaniği

Gerçek kriz sürecinin temel stat'ı. 40'tan başlar, event seçimlerine göre artar veya azalır. 0-100 arasında clamp edilir.

| Durum | Sonuç |
|-------|-------|
| controlStat = 0 | Anında başarısız (süreç bitmeden) |
| Süreç sonu, controlStat < 50 | Başarısız |
| Süreç sonu, controlStat >= 50 | Başarılı → pazarlık aşaması |

Oyuncunun amacı 5 dakika boyunca controlStat'ı 50'nin üzerinde tutmak. Eventlere verilen seçimler controlStat'ı artırabilir veya düşürebilir.

---

## Pazarlık

Süreç başarılı bittiğinde oyuncunun ne kadar kazanacağı controlStat'a göre belirlenir:

- `bargainingPower = (controlStat - 50) / 50` → 0.0 ile 1.0 arası
- Nihai kazanç: `baseReward * (0.5 + bargainingPower * 0.5)`

| controlStat | bargainingPower | Kazanç Oranı |
|-------------|-----------------|--------------|
| 50 | 0.0 | %50 |
| 75 | 0.5 | %75 |
| 100 | 1.0 | %100 |

controlStat tam sınırda (50) olsa bile oyuncu başarılı sayılır ama kazancı minimum düzeydedir.

Pazarlık veya game over ekranı gösterildiğinde oyun duraklatılır (`GameManager.PauseGame()`). Oyuncu ekranı kapatana kadar hiçbir sistem çalışmaz. UI `DismissResultScreen()` çağırdığında stat'lar uygulanır ve oyun devam eder.

---

## Sahte Kriz ve Offer Tekrar Sistemi

Offer event'leri Inspector'da `isFakeCrisis = true/false` olarak belirlenir. İlk gelişte offer tam olarak belirlendiği gibi davranır.

**Gerçek offer (isFakeCrisis = false):**
- Oyuncu kabul ederse gerçek kriz süreci başlar (ActiveProcess).
- Kabul edildiğinde havuzdan **kalıcı olarak** çıkar — bir daha asla gelmez.
- Reddedilirse havuzda kalır, tekrar gelebilir (yine gerçek olarak).

**Sahte offer (isFakeCrisis = true):**
- İlk gelişte **kesinlikle** sahte kriz olarak çalışır.
- Oyuncu kabul edip sahte olduğu ortaya çıktıktan sonra `revealedFakeOffers`'a eklenir.
- Tekrar geldiğinde `realChanceOnRepeat` değerine göre gerçek kriz olarak gelebilir (veya yine sahte olabilir).
- Gerçek olarak gelip kabul edilirse → gerçek kriz süreci başlar, havuzdan kalıcı olarak çıkar.

**Art arda tekrar engeli:** Aynı offer arka arkaya iki kez gelemez (`lastOffer` takibi).

**Inspector alanları (Offer tipinde, isFakeCrisis = true iken):**
- `realChanceOnRepeat` (0-1): Sahte kriz ortaya çıktıktan sonra tekrar geldiğinde gerçek olma olasılığı. 0 = hep fake, 1 = hep gerçek. Varsayılan 0.3.
- `fakeCrisisEvents`: Sahte kriz event zinciri (sadece fake olarak çalıştığında kullanılır).

Oyuncunun sahte krizi gerçek krizden ayırt edecek bilgiye sahip olması beklenir (event açıklaması, ipuçları vb.).

---

## Anti-Overlap (EventCoordinator)

Please Paper event'leri ve RandomEventManager aynı anda event gösteremez. `EventCoordinator` static sınıfı paylaşımlı bir slot yönetir:

- Teklif gelmeden önce slot kontrol edilir → doluysa teklif ertelenir
- RandomEventManager event tetiklemeden önce slot kontrol eder → doluysa bu cycle atlanır
- Event/teklif çözüldüğünde slot serbest bırakılır

Bu sayede iki sistem birbirini kesintiye uğratmaz.

---

## Bütçe ve Sonuç

Please Paper'da peşin ödeme yoktur. Kazanç veya zarar tamamen süreç sonucuna göre hesaplanır.

| Durum | Wealth Değişimi | Suspicion Değişimi |
|-------|-----------------|-------------------|
| Gerçek kriz başarı | `+baseReward * kazançOranı - eventCosts` | `+eventSuspicion` |
| Gerçek kriz başarısızlık | `-eventCosts` | `+eventSuspicion` |
| Sahte kriz | `-eventCosts` | `+eventSuspicion` |

Sonrasında cooldown başlar (MiniGameData.cooldownDuration).

---

## Event Sistemi

Her Offer event'inin kendi `processEvents` havuzu vardır. Gerçek kriz başladığında bu havuz aktif havuz olarak atanır. Offer'ın `processEvents`'i boşsa `database.processEvents` yedek olarak kullanılır.

Gerçek kriz sürecinde her 15 saniyede bir aktif havuzdan rastgele bir event seçilir. Aynı event bir süreçte iki kez tetiklenmez. Havuz sabit kalır — seçenek sonucuna göre değişmez.

Her event seçeneği controlStat değişimi (`controlStatModifier`), şüphe (`suspicionModifier`) ve maliyet (`costModifier`) içerebilir.

Sahte krizde ise eventler zincir halinde sıralı gösterilir — havuzdan rastgele seçim yapılmaz.

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `OnOfferReceived` | Teklif geldi (Offer event) |
| `OnOfferDecisionTimerUpdate` | Teklif karar sayacı güncellendi |
| `OnProcessStarted` | Süreç başladı (offer, süre) |
| `OnProcessProgress` | Her frame ilerleme (0-1) |
| `OnControlStatChanged` | controlStat değişti |
| `OnPleasePaperEventTriggered` | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | Event karar sayacı güncellendi |
| `OnPleasePaperEventResolved` | Oyuncu event seçimi yaptı |
| `OnBargainingStarted` | Pazarlık başladı, oyun duraklatıldı (bargainingPower). UI `DismissResultScreen()` çağırmalı |
| `OnGameOver` | Süreç başarısız bitti, oyun duraklatıldı (sebep mesajı). UI `DismissResultScreen()` çağırmalı |
| `OnProcessCompleted` | Sonuç ekranı kapatıldı, stat'lar uygulandı (başarı, başarısızlık veya sahte kriz) |
| `OnPleasePaperFailed` | Minigame başlatılamadı |

---

## Metodlar

| Metod | İşlev |
|-------|-------|
| `AcceptOffer()` | Teklifi kabul et |
| `RejectOffer()` | Teklifi reddet |
| `ResolveEvent(choiceIndex)` | Event seçimi yap |
| `DismissResultScreen()` | Pazarlık/game over ekranını kapat, stat uygula, oyunu devam ettir |
| `IsActive()` | Minigame aktif mi |
| `GetCurrentState()` | Mevcut state |
| `GetControlStat()` | controlStat değeri |
| `GetProcessProgress()` | Süreç ilerleme oranı (0-1) |
