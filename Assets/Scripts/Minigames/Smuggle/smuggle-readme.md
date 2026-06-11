# Smuggle Minigame

Madenler skill dalına bağlı kaçakçılık operasyonu minigame'i.

---

## Durum Makinesi

```
                     CancelSelection()
                    ┌──────────────────── Idle
                    │                      │
Idle ──→ SelectingRoute ──→ SelectingCourier ──→ InProgress ←──→ EventPhase
                                                     │               │
                                                     │          causesFailure
                                                     │          failureDelay
                                                     │               │
                                                     ├── FailOperation() ──→ Idle
                                                     │
                                                     ├── CancelOperation() ──→ Idle
                                                     │
                                                     ▼
                                                CalculateResult ──→ Idle
```

| Durum | Açıklama |
|-------|----------|
| Idle | Beklemede, yeni operasyon başlatılabilir |
| SelectingRoute | Oyuncu rota seçiyor (4 rota sunulur) |
| SelectingCourier | Oyuncu kurye seçiyor (3 kurye sunulur), bütçe kontrolü yapılır |
| InProgress | Operasyon devam ediyor, kurye yolda |
| EventPhase | Event tetiklendi, oyuncu karar bekliyor (10s) |

---

## Akış

```
Skill açılır
    │
    ▼
UnlockMinigameEffect → MinigameManager.UnlockMinigame()
    │
    ▼
TryStartMinigame() → açık mı? cooldown'da mı? aktif mi?
    │
    ├── Önceki seçenekler duruyorsa → aynı rota paketi + kuryeler sunulur
    │
    ├── Seçenekler yoksa → StartSelectionPhase() → rastgele 1 rota paketi + 3 kurye
    │
    ▼
Seçim ekranı
    │
    ├── CancelSelection() → Idle (seçenekler korunur, para ödenmedi)
    │
    ├── SelectRoute(route)
    │
    ├── SelectCourier(courier) → bütçe kontrolü (route.cost + courier.cost)
    │       │
    │       ├── Para yeterli → peşin düşülür → StartOperation()
    │       │
    │       └── Para yetersiz → OnSmuggleFailed
    │
    ▼
StartOperation() → süre hesaplanır, zamanlayıcılar başlar
    │
    ├── Her 5s → TryTriggerEvent()
    │       │
    │       ├── Event tetiklendi → EventPhase (10s karar süresi)
    │       │       │
    │       │       ├── causesFailure → anında FailOperation()
    │       │       │
    │       │       ├── failureDelay > 0 → X saniye sonra FailOperation()
    │       │       │
    │       │       ├── Normal seçim → modifier birikir, havuz güncellenir
    │       │       │
    │       │       └── Süre doldu → ilk seçenek otomatik seçilir
    │       │
    │       └── Event tetiklenmedi → devam
    │
    ├── pendingFailureTimer doldu → FailOperation()
    │
    ├── CancelOperation() → kısmi iade, Idle
    │
    ▼
Kurye hedefe ulaştı → CalculateResult() → her zaman başarı
    │
    ├── Kazanç ve şüphe uygulanır
    └── Cooldown başlar
```

---

## Başarı / Başarısızlık

Başarı tamamen eventlere bağlıdır. Zar atılmaz. Kurye yolun sonuna ulaştıysa operasyon her zaman başarılıdır. Başarısızlık sadece yol ortasında event sonucu gerçekleşir:

- `causesFailure = true` → operasyon anında biter
- `failureDelay > 0` → X saniye sonra operasyon biter (kalan süreye clamp edilir, varıştan en az 1s önce patlar, EventPhase sırasında sayaç duraklar)

Maliyet operasyon başında peşin ödendiği için başarısızlıkta iade yapılmaz, sadece event kayıpları ek zarar olarak uygulanır.

---

## Bütçe ve Sonuç

`SelectCourier` aşamasında `route.cost + courier.cost` peşin ödenir. Para yetersizse `OnSmuggleFailed` tetiklenir.

| Durum | Wealth Değişimi | Suspicion Değişimi |
|-------|-----------------|-------------------|
| Başarı | `+baseReward - eventCosts` | `riskLevel * 0.1 + eventSuspicion` |
| Başarısızlık | `-eventCosts` | `riskLevel * 0.3 + eventSuspicion` |

Sonrasında cooldown başlar (MiniGameData.cooldownDuration).

---

## İptal

Seçim aşamasında `CancelSelection()` ile iptal edilebilir. Para ödenmemiştir, iade yoktur. Seçenekler korunur — panel tekrar açıldığında aynı rota paketi ve kuryeler sunulur.

Operasyon sırasında `CancelOperation()` ile iptal edilebilir. İade formülü: `totalCost * (kalanYüzde / 2)`. Seçenekler temizlenir — sonraki açılışta yeni seçenekler gelir.

---

## Seçenek Kalıcılığı

Rota paketi ve kurye seçenekleri operasyon başlatılana kadar korunur. Panel kapatıp açınca aynı seçenekler sunulur. Operasyon bittikten, başarısız olduktan veya iptal edildikten sonra `ClearSelectionData()` ile temizlenir, sonraki açılışta yeni seçenekler oluşturulur.

---

## Event Sistemi

Her 5 saniyede bir aktif havuzdan rastgele bir event adayı seçilir. Adayın `triggerType`'ına göre tetiklenme şansı hesaplanır:

| Trigger | Formül |
|---------|--------|
| Risk | `riskLevel * (1 - reliability/200)` |
| Betrayal | `betrayalChance * 100` |
| Incompetence | `100 - reliability` |

Aynı event bir operasyonda iki kez tetiklenmez. Yüksek reliability, Risk eventlerinin tetiklenme şansını da düşürür. Oyuncu seçiminde `nextEventPool` varsa aktif havuz değişir (event zinciri). `nextEventPool` boş veya null ise zincir biter.

Her event seçeneği şüphe (`suspicionModifier`), maliyet (`costModifier`), anında başarısızlık (`causesFailure`), gecikmeli başarısızlık (`failureDelay`) ve sonraki event havuzu (`nextEventPool`) içerebilir. `causesFailure` ve `failureDelay` aynı anda kullanılmaz.

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `OnSelectionPhaseStarted` | Rota paketi ve kurye seçenekleri hazır |
| `OnOperationStarted` | Operasyon başladı |
| `OnOperationProgress` | Her frame ilerleme (0-1) |
| `OnSmuggleEventTriggered` | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | Event karar sayacı güncellendi |
| `OnSmuggleEventResolved` | Oyuncu event seçimi yaptı |
| `OnOperationCompleted` | Operasyon bitti (başarı veya başarısızlık) |
| `OnOperationCancelled` | Operasyon iptal edildi |
| `OnSmuggleFailed` | Başlatma başarısız |

---

## Metodlar

| Metod | İşlev |
|-------|-------|
| `TryStartMinigame()` | Minigame'i başlatmayı dener (açık mı, cooldown, aktif mi) |
| `SelectRoute(route)` | Oyuncu rota seçti |
| `SelectCourier(courier)` | Oyuncu kurye seçti, bütçe kontrolü + peşin ödeme, operasyon başlar |
| `ResolveEvent(choiceIndex)` | Oyuncu event seçimi yaptı |
| `CancelSelection()` | Seçim aşamasını iptal eder (seçenekler korunur) |
| `CancelOperation()` | Operasyonu iptal eder, kısmi iade yapar |
| `CanPlay()` | Minigame oynanabilir mi |
| `GetOperationProgress()` | Operasyon ilerleme oranı (0-1) |
| `GetCurrentState()` | Mevcut state |
