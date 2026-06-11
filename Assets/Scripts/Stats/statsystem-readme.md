# Stat Sistemi

Oyuncunun temel istatistiklerini yöneten sistem.

---

## Statlar

| Stat | Aralık | Açıklama |
|------|--------|----------|
| **Wealth** | 0 - ∞ | Para, sınırsız |
| **Suspicion** | 0 - 100 | Şüphe, 100'e ulaşınca game over |
| **Reputation** | 0 - 100 | İtibar, şüphe artış hızını etkiler |
| **PoliticalInfluence** | -100 / +100 | Siyasi nüfuz, skill verimini etkiler |

---

## Stat Etkileşimleri

### Şüphe + İtibar

```
Şüphe artışı = miktar × GetSuspicionMultiplier()

İtibar 0   → çarpan 1.5 (şüphe %50 fazla artar)
İtibar 50  → çarpan 1.0 (normal)
İtibar 100 → çarpan 0.5 (şüphe %50 az artar)
```

### Siyasi Nüfuz + Skill Verimi

```
Skill verimi = GetSkillEfficiencyMultiplier()

Nüfuz -100 → çarpan 0.5 (skill'ler yarım verimli)
Nüfuz 0    → çarpan 1.0 (normal)
Nüfuz +100 → çarpan 1.5 (skill'ler %50 daha verimli)
```

---

## Kullanım

### Stat Değiştirme

```csharp
//genel kullanım (şüphede çarpan uygulanır)
GameStatManager.Instance.ModifyStat(StatType.Suspicion, 10f);
GameStatManager.Instance.AddSuspicion(10f);

//çarpansız şüphe ekleme (özel durumlar için)
GameStatManager.Instance.AddSuspicionRaw(10f);

//diğer statlar
GameStatManager.Instance.AddReputation(5f);
GameStatManager.Instance.AddPoliticalInfluence(-10f);
GameStatManager.Instance.AddWealth(1000f);
```

### Modifier Sorgulama

```csharp
float suspicionMult = GameStatManager.Instance.GetSuspicionMultiplier();
float skillEfficiency = GameStatManager.Instance.GetSkillEfficiencyMultiplier();
```

### Event Dinleme

```csharp
GameStatManager.OnStatChanged += (statType, oldVal, newVal) => {
    //stat değişti
};

GameStatManager.OnGameOver += () => {
    //şüphe 100'e ulaştı
};
```

---

## Kim Neyi Yapar?

| İş | Sorumlu |
|----|---------|
| Stat değerlerini tutma | `GameStatManager` |
| Şüphe çarpanı hesaplama | `GetSuspicionMultiplier()` |
| Skill verimi çarpanı hesaplama | `GetSkillEfficiencyMultiplier()` |
| Game over tetikleme | `AddSuspicion()` / `SetStat()` |
| Skill/Event'ten stat değiştirme | `StatModifierEffect` |

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `OnStatChanged` | Herhangi bir stat değiştiğinde |
| `OnGameOver` | Şüphe 100'e ulaştığında |
