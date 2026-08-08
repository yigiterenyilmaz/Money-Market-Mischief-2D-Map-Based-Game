# Güven (Trust) — YARIM SİSTEM

> **Bu dosya bir uyarıdır.** Güven stat'ı birikiyor ama **hiçbir şeye yaramıyor**.
> Riski azaltma davranışı henüz yazılmadı. Aşağıdaki skiller bu yüzden şu an
> "para harcayıp görünmez bir sayı artıran" skiller durumunda.

## Güven nedir

Güven, **riske karşı tampondur**: yüksek güven, kötü sonuçların oyuncuya çarpma
şiddetini azaltmalıdır. Reputation (itibar) ile **karıştırılmamalıdır** —
`StatType.Reputation` enum'da "eski Trust" diye notlanmıştır, o AYRI bir stattır.
Bu isim bir kez yeniden kullanıldığı için karışıklığa açık; kod okurken dikkat.

## Şu an ne var (bitti)

- `StatType.Trust` — enum'un **sonuna** eklendi (indeks 4). Araya eklenmedi, çünkü
  asset'ler `statType`'ı indeks olarak saklar; araya ekleme tüm mevcut skill/event
  asset'lerini sessizce kaydırırdı.
- `GameStatManager` içinde tam tesisat: `startingTrust`, `minTrust`/`maxTrust`,
  `Trust` getter'ı, `AddTrust()`, `ModifyStat`/`SetStat`/`GetStatPercent` kolları,
  kalıcı çarpan desteği (`trustGainMultiplier`) ve `OnStatChanged` yayını.
- Yani güven **birikiyor, sınırlanıyor ve event yayıyor**. Diğer statlarla eşit
  davranıyor.

## Şu an ne YOK (yapılacak)

1. **Güvenin riski azaltması.** Asıl iş bu. Doğal yeri `GameStatManager` içinde,
   `GetSuspicionMultiplier()` ve `GetSkillEfficiencyMultiplier()`'ın hemen yanı —
   risk çarpanları zaten orada yaşıyor. Muhtemel biçim:
   `GetRiskDampening()` → güven arttıkça 1'den küçülen bir çarpan, ve bunun
   olumsuz sonuçlara (şüphe artışı, minigame başarısızlığı, event cezaları)
   uygulanması.
2. **UI'da gösterim.** Güven hiçbir ekranda görünmüyor. Oyuncu artan bir sayıyı
   göremediği için skiller ödülsüz hissettiriyor.
3. **Denge sayıları.** Aşağıdaki skillerin verdiği güven miktarları uydurmadır.

## Bu sisteme bağlı skiller

Emlak (Finance/RealEstate) ağacının a34 kolu tamamen güven üzerine kurulu:

| Skill | Not | Durum |
|-------|-----|-------|
| `a34` | "emlak güven root - cüzi kira yardımı" | **Bağlandı** — `StatModifierEffect(Trust, +10)`. +10 uydurma bir sayıdır. |
| `a36` | "kira ödeme" — insanlara kira ödeyip güven kazanma | Bekliyor |
| `a39` | "ev alma" — insanlara ev alıp güven kazanma | Bekliyor |
| `a35` | "yasa tasarısı" — hangi minigame olduğu hatırlanamadı | Tasarım bekliyor |

## Özet

Güven **taşınıyor ama harcanmıyor**. Yukarıdaki 1. madde yazılana kadar a34 (ve
sonra a36/a39) oyuncu için gözle görülür bir fayda üretmez.
