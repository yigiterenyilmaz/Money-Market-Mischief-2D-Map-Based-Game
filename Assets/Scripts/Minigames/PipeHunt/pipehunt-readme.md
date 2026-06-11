# PipeHunt Minigame — Sistem Dokumantasyonu

## Genel Bakis

Oyuncu zeminde gizlenmiş boruları bulup aletiyle kırarak petrol geliri elde eder. Süre dolduğunda oyun bitmez — oyuncu kalıp daha fazla boru kırmaya devam edebilir, ancak kalma süresine orantılı olarak şüphe artar. Şüphe %100'e ulaşırsa tüm oyun biter (game over).

---

## Dosya Yapisi

```
PipeHunt/
├── PipeHuntManager.cs    — Ana yönetici (state machine, oyun döngüsü, eventler)
├── PipeHuntDatabase.cs   — Inspector ayarları (ScriptableObject)
├── PipeType.cs           — Boru tipi tanımı (ScriptableObject)
├── HuntTool.cs           — Alet tipi tanımı (ScriptableObject)
├── PipeInstance.cs       — Tek bir borunun runtime verisi
└── pipehunt-readme.md    — Bu dosya
```

---

## State Machine

```
  Idle ──→ Active ──→ Overtime ──→ Finished ──→ Idle
            │                        │
            │    PlayerLeft          │    PlayerLeft
            │         │              │    GameOver
            │         ▼              │       │
            └──→ Finished ←─────────┘       │
                    │                        │
                    ▼                        ▼
                  Idle               GameManager.EndGame()
```

Not: Alet kırılınca oyun bitmez — sadece vuruş yapılamaz. Patlayan borular gelir üretmeye devam eder, oyuncu istediğinde LeaveGame ile çıkar.

| State | Aciklama | Timer | Gelir | Suphe | Oyuncu Aksiyonlari |
|---|---|---|---|---|---|
| **Idle** | Minigame aktif değil | - | - | - | StartGame(tool) |
| **Active** | Süre içinde oynuyor | Geri sayım | Birikiyor | Yok | HitPipe, HitEmpty, LeaveGame |
| **Overtime** | Süre doldu, oyuncu kalmaya devam ediyor | Durdu | Birikiyor | Artıyor | HitPipe, HitEmpty, LeaveGame |
| **Finished** | Geçiş anı (anlık) | - | - | - | - |

---

## Lifecycle (Yasam Dongusu)

### 1. Oyun Oncesi — Alet Secimi

```
UI: GetAvailableTools()         → List<HuntTool> (alet listesi)
UI: GetToolDuration(tool)       → float (bu aletle oyun süresi)

Oyuncu aleti secer → UI: StartGame(selectedTool)
```

### 2. StartGame(HuntTool tool)

Oyuncu alet secim ekraninda bir alet secip "Basla" butonuna bastiginda UI bu metodu cagirir.
Seçilen aletin maliyeti kesilir, borular yerlestirilir, sure hesaplanir ve minigame baslar.

Sırasıyla şunlar olur:

```
1. State kontrolu (Idle değilse → return)
2. Null kontrolu (tool == null → return)
3. MinigameManager kontrolleri:
   - IsMinigameUnlocked(minigameData) → false ise return
   - IsOnCooldown(minigameData) → true ise return
4. Alet maliyeti odeme:
   - HasEnoughWealth(tool.cost) → false ise return
   - TrySpendWealth(tool.cost) → wealth kesilir
5. Boru uretimi (GeneratePipes):
   - database.pipeTypes'tan rastgele tip secer
   - Random.Range(minPipeCount, maxPipeCount+1) kadar boru olusturur
   - Her boru icin normalize (0-1) pozisyon uretir
   - Borular arasi minimum mesafe kontrolu yapar (minPipeDistance)
   - 100 deneme icinde pozisyon bulamazsa o boruyu atlar
6. Alet ve sure ayarlari:
   - toolRemainingDurability = tool.durability
   - gameDuration = Lerp(minGameDuration, maxGameDuration, tool.stealth)
   - gameTimer = gameDuration
7. State → Active
8. GameManager.PauseGame() → Time.timeScale = 0
9. OnGameStarted event (boruların kopyası, sure, alet)
```

### 3. Active State — Her Frame (Update)

```
gameTimer -= Time.unscaledDeltaTime
OnTimerUpdate(gameTimer)
AccumulateIncome()    → patlayan boruların gelirini toplar

if gameTimer <= 0:
    EnterOvertime()
```

### 4. Oyuncu Etkilesimi

**Boruya vurus — HitPipe(pipeId):**

Oyuncu ekrana dokundu ve dokunma noktasi bir borunun uzerine denk geldi.
UI dokunma pozisyonunu kontrol eder, bir boruya denk geldiyse o borunun id'sini bu metoda gonderir.

```
1. Alet kırıksa (toolRemainingDurability <= 0) → return
2. Boru bulunur (id ile aranır)
3. Zaten patlaksa → return
4. pipe.remainingDurability -= tool.damagePerHit
5. toolRemainingDurability -= 1 (her vuruş 1 düşürür)
6. OnToolDamaged(kalanDayaniklilik)

   Boru patladıysa (remainingDurability <= 0):
     → isBurst = true
     → burstTime = Time.unscaledTime
     → OnPipeBurst(pipe)
   Patlamadıysa:
     → OnPipeHit(pipe, kalanDayaniklilik)

7. Alet kırıldıysa (toolRemainingDurability <= 0):
     → OnToolBroken (oyun devam eder, sadece vuruş yapılamaz)
```

**Bos zemine vurus — HitEmpty():**

Oyuncu ekrana dokundu ama dokunma noktasi hicbir borunun uzerinde degil.
UI dokunma pozisyonunu kontrol eder, hicbir boruya denk gelmediyse bu metodu cagirir.
Alet yine asinir ama hicbir boru hasar almaz — bosa vurus.

```
1. Alet kırıksa (toolRemainingDurability <= 0) → return
2. toolRemainingDurability -= 1
3. OnToolDamaged(kalanDayaniklilik)
4. OnEmptyHit(kalanDayaniklilik)
5. Alet kırıldıysa → OnToolBroken (oyun devam eder, sadece vuruş yapılamaz)
```

**Oyuncu cikis — LeaveGame():**

Oyuncu "Cik" butonuna bastiginda UI bu metodu cagirir.
Sure dolmadan da cikabilir (kazancini alir), overtime'da da cikabilir (suphe durur).
Minigame sonlanir, gelir wealth'e eklenir, ana oyun devam eder.

```
→ FinishGame(PlayerLeft)
```

### 5. Overtime State — Süre Doldu

```
EnterOvertime():
  State → Overtime
  overtimeElapsed = 0
  OnOvertimeStarted event

Her frame:
  overtimeElapsed += Time.unscaledDeltaTime
  AccumulateIncome()       → gelir birikimine devam
  AccumulateSuspicion()    → şüphe eklemeye başla
```

Oyuncu Overtime'da da HitPipe, HitEmpty, LeaveGame yapabilir — oyun mekaniği aynıdır. Fark: şüphe artmaya başlar.

### 6. Suphe Hesabi (AccumulateSuspicion)

```
overtimePercent = (overtimeElapsed / gameDuration) * 100

targetSuspicion = suspicionBase * pow(overtimePercent / 100, suspicionExponent)

delta = targetSuspicion - totalSuspicionAdded
if delta > 0:
    totalSuspicionAdded = targetSuspicion
    GameStatManager.AddSuspicion(delta)
    OnOvertimeUpdate(overtimeElapsed, totalSuspicionAdded)
```

**Formul:** `suphe = base * (asimYuzdesi / 100) ^ exponent`

**Ornek (base=50, exponent=2):**

| Overtime Suresi | Oyun Suresi | Asim % | Toplam Suphe |
|---|---|---|---|
| 3 sn | 30 sn | %10 | 0.5 |
| 9 sn | 30 sn | %30 | 4.5 |
| 15 sn | 30 sn | %50 | 12.5 |
| 30 sn | 30 sn | %100 | 50 |
| 60 sn | 30 sn | %200 | 200 |

Exponent arttikca egri daha agresif olur (basta cok yavas, sonda cok hizli).

### 7. Game Over Mekanizmasi

```
GameStatManager.AddSuspicion(delta)
  → suphe >= maxSuspicion (100) ise:
    → GameStatManager.OnGameOver event tetiklenir
    → GameManager.EndGame() → Time.timeScale = 0, GameState.GameOver
    → PipeHuntManager.HandleGameOver() → FinishGame(GameOver)
```

GameOver durumunda `ResumeGame()` cagirilmaz — oyun bitmiştir.

### 8. FinishGame(PipeHuntEndReason reason)

```
1. State → Finished
2. Patlayan boru sayisi hesaplanir
3. GameStatManager.AddWealth(accumulatedIncome)
4. MinigameManager.StartCooldown(minigameData)
5. PipeHuntResult olusturulur:
   - totalIncome, burstPipeCount, totalPipeCount
   - endReason, remainingTime
   - toolUsed, toolCostPaid
   - overtimeElapsed, suspicionAdded
6. OnGameFinished(result)
7. if reason != GameOver → GameManager.ResumeGame() → timeScale = 1
8. State → Idle, pipes temizlenir
```

---

## Gelir Hesabi (AccumulateIncome)

Her frame, patlayan her boru icin:
```
gelir += pipeType.incomePerSecond * Time.unscaledDeltaTime
```

Borular ancak patladiktan sonra gelir uretmeye baslar. Ne kadar erken patlatirsan o kadar cok gelir birikir.

**Ornek:** 3 boru patlamis, hepsi 5/sn gelirli, 10 saniye gecmis → 3 * 5 * 10 = 150 gelir.

Gercekte her boru farkli zamanda patlar, bu yuzden hesap frame-bazli birikim seklindedir.

---

## Alet Sistemi

| Alan | Aciklama |
|---|---|
| `cost` | Her oyun basinda odenen ucret (wealth) |
| `durability` | Toplam vuruş hakki (her vurus 1 dusurur) |
| `damagePerHit` | Vuruş basina boruya verilen hasar |
| `stealth` | 0-1 arasi. Sure formulu: `Lerp(minDuration, maxDuration, stealth)` |

**Stealth trade-off:**
- stealth=0.1 → kisa sure, genelde guclu/pahali alet
- stealth=0.9 → uzun sure, genelde zayif/ucuz alet

Alet her vuruşta 1 dayaniklilik kaybeder — boruya vursa da bosa vursa da. Alet kirilinca vuruş yapilamaz ama oyun devam eder — patlayan borular gelir uretmeye devam eder, oyuncu istediginde cikar.

---

## Boru Sistemi

| Alan | Aciklama |
|---|---|
| `durability` | Borunun toplam dayanikliligi |
| `incomePerSecond` | Patladiktan sonra saniyede kazandirdigi gelir |

**Vuruş hesabi:** `pipe.remainingDurability -= tool.damagePerHit`

Ornek: Boru dayanikliligi=20, alet hasari=8 → 3 vurus gerekir (8+8+8=24 > 20).

Her boru oyun basinda rastgele tipte olusturulur. Pozisyonlar normalize (0-1) koordinatlardir — UI bunlari ekran pozisyonuna cevirir.

---

## Zaman Yonetimi

- Minigame basladiginda `GameManager.PauseGame()` cagirilir → `Time.timeScale = 0`
- Tum timerlar `Time.unscaledDeltaTime` ve `Time.unscaledTime` kullanir
- Minigame bittiginde `GameManager.ResumeGame()` cagirilir → `Time.timeScale = 1`
- Game over durumunda ResumeGame cagirilmaz (oyun zaten bitmistir)
- Ana oyunun tum sistemleri (feed, random events, cooldownlar) minigame sirasinda durur

---

## Event Tablosu

| Event | Parametreler | Ne Zaman | UI Ne Yapar |
|---|---|---|---|
| `OnGameStarted` | `List<PipeInstance>, float, HuntTool` | Oyun basladiginda | Zemini, borulari ve timer'i cizer |
| `OnTimerUpdate` | `float` (kalan sure) | Active'de her frame | Timer gostergesini gunceller |
| `OnPipeHit` | `PipeInstance, int` (kalan dayaniklilik) | Boru vuruldu ama patlamadi | Vuruş efekti gosterir |
| `OnPipeBurst` | `PipeInstance` | Boru patladi | Patlama efekti, gelir gosterir |
| `OnEmptyHit` | `int` (kalan alet dayanikliligi) | Bos zemine vuruldu | Boş vuruş efekti |
| `OnToolDamaged` | `int` (kalan dayaniklilik) | Her vurusta | Alet dayaniklilik barini gunceller |
| `OnToolBroken` | - | Alet kirildigi anda | Kirilma efekti gosterir |
| `OnIncomeUpdate` | `float` (toplam gelir) | Gelir biriktikce | Gelir sayacini gunceller |
| `OnOvertimeStarted` | - | Sure doldu | Uyari UI gosterir |
| `OnOvertimeUpdate` | `float, float` (overtime suresi, toplam suphe) | Overtime'da her frame | Suphe barini gunceller |
| `OnGameFinished` | `PipeHuntResult` | Oyun bittiginde | Sonuc ekrani gosterir |

---

## Getter Metodlari (UI Icin)

| Metod | Donus | Aciklama |
|---|---|---|
| `GetAvailableTools()` | `List<HuntTool>` | Secilabilir aletler |
| `GetToolDuration(tool)` | `float` | Bu aletle oyun suresi |
| `GetCurrentState()` | `PipeHuntState` | Mevcut state |
| `GetPipes()` | `List<PipeInstance>` (kopya) | Boruların anlık durumu |
| `GetToolRemainingDurability()` | `int` | Alet kalan dayaniklilik |
| `GetAccumulatedIncome()` | `float` | Biriken toplam gelir |
| `GetRemainingTime()` | `float` | Kalan sure (Active'de) |
| `GetCurrentTool()` | `HuntTool` | Secilen alet |
| `GetOvertimeElapsed()` | `float` | Overtime'da gecen sure |
| `GetTotalSuspicionAdded()` | `float` | Eklenen toplam suphe |

---

## PipeHuntResult (Sonuc Verisi)

| Alan | Tip | Aciklama |
|---|---|---|
| `totalIncome` | float | Toplam kazanilan gelir |
| `burstPipeCount` | int | Patlayan boru sayisi |
| `totalPipeCount` | int | Toplam boru sayisi |
| `endReason` | PipeHuntEndReason | PlayerLeft / GameOver |
| `remainingTime` | float | Active'de ciktiysa kalan sure |
| `toolUsed` | HuntTool | Kullanilan alet |
| `toolCostPaid` | int | Odenen alet maliyeti |
| `overtimeElapsed` | float | Overtime'da ne kadar kalindi |
| `suspicionAdded` | float | Eklenen toplam suphe |

---

## Unity Kurulumu

### 1. Asset Olusturma

```
Project > sag tik > Create > Minigames > PipeHunt > PipeType    (boru tipleri)
Project > sag tik > Create > Minigames > PipeHunt > Tool        (alet tipleri)
Project > sag tik > Create > Minigames > PipeHunt > Database    (ana ayarlar)
Project > sag tik > Create > Minigames > MiniGame Data          (unlock/cooldown)
```

### 2. Sahne Kurulumu

```
Hierarchy > Create Empty > "PipeHuntManager"
  → Add Component > PipeHuntManager
  → Inspector:
      minigameData: [MiniGameData asset suruklenir]
      database:     [PipeHuntDatabase asset suruklenir]
```

### 3. Database Inspector Ornek Degerler

```
[Borular]
pipeTypes: copper_pipe, steel_pipe, gold_pipe
minPipeCount: 5
maxPipeCount: 10

[Aletler]
tools: shovel, pickaxe, jackhammer

[Sure]
minGameDuration: 15
maxGameDuration: 45

[Boru Yerlesim]
minPipeDistance: 0.1

[Sure Asimi — Suphe]
suspicionBase: 50
suspicionExponent: 2
```

---

## Dis Sistem Bagimliliklari

| Sistem | Nasil Kullaniliyor |
|---|---|
| `GameManager` | PauseGame() / ResumeGame() — minigame sirasinda ana oyunu durdurur |
| `GameStatManager` | AddWealth() — gelir ekleme, TrySpendWealth() — alet maliyeti, AddSuspicion() — overtime suphesi, OnGameOver event — game over dinleme |
| `MinigameManager` | IsMinigameUnlocked() / IsOnCooldown() / StartCooldown() — erisim ve cooldown kontrolu |
