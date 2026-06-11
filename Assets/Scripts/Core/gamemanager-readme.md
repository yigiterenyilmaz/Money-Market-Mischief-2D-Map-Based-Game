# Game Manager

Oyunun merkezi durum yöneticisi.

---

## Oyun Durumları

```
Initializing ──→ Playing ←──→ Paused
                    │
                    ▼
                 GameOver
```

| Durum | timeScale | Açıklama |
|-------|-----------|----------|
| Initializing | - | Oyun hazırlanıyor (harita üretimi vb.) |
| Playing | 1 | Oyun aktif, tüm timer'lar çalışıyor |
| Paused | 0 | Oyun duraklatıldı, Time.deltaTime = 0 |
| GameOver | 0 | Şüphe 100'e ulaştı, her şey durdu |

---

## Akış

```
Oyun açılır
    │
    ▼
Initializing
    │ (ileride harita üretimi burada olacak)
    ▼
StartGame() → Playing → zaman akar
    │                       │
    ├── PauseGame()    ←── Paused
    │   ResumeGame()   ──→ Playing
    │
    └── Şüphe 100
            │
            ▼
        GameStatManager.OnGameOver
            │
            ▼
        GameManager.EndGame() → GameOver
```

---

## Diğer Sistemlerle İlişki

GameManager hiçbir sisteme doğrudan müdahale etmez. `Time.timeScale = 0` yapıldığında tüm `Time.deltaTime` kullanan sistemler otomatik durur:
- SocialMediaManager (feed timer'ları)
- ElectionManager (seçim timer'ı)
- SkillTreeManager (pasif gelir)

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `OnGameStateChanged` | Herhangi bir state değişiminde |
| `OnGameStarted` | Oyun başladığında |
| `OnGamePaused` | Oyun duraklatıldığında |
| `OnGameResumed` | Oyun devam ettiğinde |
| `OnGameEnded` | Game over olduğunda |

---

## Metodlar

| Metod | İşlev |
|-------|-------|
| `StartGame()` | Oyunu başlatır |
| `PauseGame()` | Oyunu duraklatır |
| `ResumeGame()` | Oyunu devam ettirir |
| `EndGame()` | Game over |
| `IsPlaying()` / `IsPaused()` / `IsGameOver()` | Durum sorgulama |
| `GetGameTime()` | Toplam oynanan süre (saniye) |
| `GetGameTimeMinutes()` | Toplam oynanan süre (dakika) |
