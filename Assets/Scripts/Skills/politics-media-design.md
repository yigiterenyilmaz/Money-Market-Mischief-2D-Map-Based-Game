# Siyaset + Medya Ağaçları — Tasarım Notları

Kaynak: kullanıcının 2026-08-12 tarihli tasarım dökümü. Bu tarihe kadar B ve C
ağacındaki 34 skill'in tamamı byte-byte aynı `PLACEHOLDER —` açıklamasını taşıyordu
(bkz. [subtree-handoff.md](subtree-handoff.md) §1). Notlar artık asset'lerin
`description` alanına yazıldı; bu dosya, tek bir skill'e sığmayan **kesitsel
mekanikleri** ve **açık kalan kararları** tutar.

---

## 1. UYARI: ağaç etiketleri ters

Kullanıcının tasarımında **B = Medya**, **C = Siyaset**. Repodaki metadata bunun
tersini söylüyor:

| | id | klasör | `SkillBranch` flag | gerçek tasarım |
|---|---|---|---|---|
| B ağacı | `b1`–`b15` | `GameData/Skills/Politics/` | `Politics` (2) | **Medya** |
| C ağacı | `c1`–`c21` | `GameData/Skills/Media/` | `Media` (4) | **Siyaset** |

Bu yalnızca bir isimlendirme hatası değil — `SkillTreeLayout.asset`'teki `branches`
alanı ağacın rengini (`SkillTreeStyle.politicsAccent` / `mediaAccent`), tooltip
etiketini (`SkillTreeTooltip.cs:219`) ve filtre butonlarını
(`SkillTreeView.cs:876`, `0=Finans, 1=Siyaset, 2=Medya`) sürüyor. Düzeltilmezse
medya skilleri Siyaset renginde ve Siyaset filtresinde görünür.

**Karar bekliyor.** Önerilen: id'lere dokunmadan sadece metadata takas edilsin —
`b*` düğümlerinin flag'i `Media`, `c*` düğümlerininki `Politics` olsun ve iki klasör
yer değiştirsin. id'leri yeniden numaralamak (`b`↔`c`) her prerequisite GUID'ini,
`SkillDatabase.asset`'i ve layout'u tek seferde bozar; getirisi yok.

## 2. Eksik düğümler

Tasarımda geçen iki skill'in asset'i **yok** — `SkillDatabase` ve
`SkillTreeLayout` de dahil hiçbir yerde tanımlı değiller:

- **`c9`** — desteklediğin partiden siyasetçi/devlet adamı olma. İmara kapalı
  alanlar mekaniği, işlemleri hızlandırma. Grafikte yeri `c7`/`c8` altı.
- **`c19`** — Mafya IV: paralel yapılanmayı def etme, pazarlık payını artırma,
  cinayette yakalanma ihtimalini azaltma, kabul oranını ve rüşvet etkinliğini
  artırma. Yeri `c18` altı.

Yeni düğüm eklemek asset + `.meta` + `SkillDatabase.asset` + `SkillTreeLayout.asset`
(satır/kolon dahil) demek; layout satır aralıkları elle açılmalı.

## 3. Sahte aktif yetenekler — silinmeyi bekliyor

`b4`, `b8`, `b13`, `c6`, `c12`, `c17` üzerinde cooldown halkası UI'ını denemek için
uydurulmuş `activeAbility` blokları var. Hepsi **eski ters etikete göre** yazılmış,
yani şimdi çifte yanlışlar:

| skill | sahte yetenek | gerçek tasarım |
|---|---|---|
| `b4` | Meydan Mitingi | Yardım yapma |
| `b8` | Kulis Pazarlığı | Trendlere yön verme |
| `b13` | Acil Kararname | Siyasi anket |
| `c6` | Manşet Patlatma | Yerel rüşvet |
| `c12` | Troll Ordusu | Yargı rüşveti |
| `c17` | Karalama Kampanyası | Mafya II |

Hepsi `StatModifierEffect` ile itibar/nüfuz oynatıyor. Gerçek efekt yazılırken
`RefIds` bloğuyla birlikte silinmeli. UI demosu hâlâ isteniyorsa tek bir düğümde
tutulup diğerleri temizlenmeli.

---

## 4. İtibar – şüphe ilişkisi

Ağacın ekonomik omurgası. Tek cümlelik kuralı:

> **Şüphe (genelde) azalmaz. İtibar, şüphenin _artma hızını_ belirler.**

Oyuncu şüpheyi doğrudan düşürmeye değil, itibarını yükselterek şüphenin artış
hızını yavaşlatmaya oynar. **Şüphe dolarsa oyun kaybedilir.**

Bunun kod tarafında karşılığı henüz yok: `GameStatManager`'da `Suspicion` ve
`Reputation` bağımsız iki sayaç. "İtibar → şüphe artış hızı" bağı yazılmadan
B ağacının yarısı (anketler, demeçler, yardım) ölçülebilir bir şey yapmaz.
Yalnızca `c12` (yargı rüşveti) ve `b12` (şüphe anketi) şüpheye doğrudan dokunur;
ikisi de kuralın istisnası olarak işaretlenmiş.

## 5. Rüşvet mekaniği

`c6` → `c10` → `c11` / `c12` zincirinin tamamı ve `c17`/`c19` (mafya) bu tek
mekaniğin üzerine biner. Rüşvet teklifi **beş sonuçtan birine** düşer:

| Tip | Sonuç |
|---|---|
| A | Rüşvet kabul edilir, hayatına devam edersin |
| B | Rüşvet zincirine düşersin |
| C | Rüşvet teklifin ifşa edilir |
| D | Paralel yapılanma çetesi |
| E | Pazarlık |

Tip A'nın çıkma şansı **politik nüfuz** ve **mafya gücü** ile artar.

Zincirde yukarı çıktıkça risk ve para birlikte büyür: `c6` şüphe riski çok düşük →
`c10` orta risk, pazarlık ve ifşa (C/E) açılır → `c11` zincir (B) açılır, risk ve
para daha da artar. `c12` paralel yapılanmayı (D) devreye sokar; `c17`/`c19` mafya
skilleri pazarlık payını, kabul oranını ve rüşvet etkinliğini yükseltir, `c19`
paralel yapılanmayı def eder.

**Rüşvet sadece bu skillere ait değil:** diğer eventlerde de bir seçenek olarak
belirebilir ve aynı beş sonuçla çalışır. Yani bu, bir skill efekti değil, ortak bir
sistem — ev kuralına göre kendi MonoBehaviour'ında yaşamalı, efektler yalnızca
üzerindeki anahtarları çevirmeli (bkz. handoff §2).

## 6. Feed manipülasyonu

B ağacının sosyal medya kolu (`b3` → `b8` → `b9` → `b10`) mevcut feed sistemine
bağlanır (`Assets/Scripts/SocialMedia/`, `feedsystem-readme.md`). Dört kaldıraç:

- kendimizi övdürme (`b3`)
- trendlere yön verme, konuyu dağıtma (`b8`)
- siyasi propaganda (`b9`)
- şantaj / ifşa / hadise (`b10`)

Feed'de zaten `TopicType` ve `TopicWeightModifierEffect` var, yani "konuyu dağıtma"
ve "propaganda" için altyapı kısmen hazır.

## 7. Haritaya bağlı içerik

- **`b5` (demeç verme):** konu, oyuncunun üzerinde bulunduğu mape göre seçilir.
- **`b4` (yardım yapma):** boş arazide işe yarar.
- **Her mapin hassas konuları olacak** — olasılıkları yüksek olmayacak ama getirileri
  yüksek olacak.

Bu üçü `MapDecorPlacer` / bölge sistemiyle konuşmayı gerektiriyor; henüz hangi
maplerin hangi hassas konuları taşıdığına dair bir tablo yok.

## 8. Sınıflandırılmamış

**"Nükleer kış"** — dökümde tek başına, bağlamsız geçiyor. Bir son-oyun olayı mı,
bir savaş minigame sonucu mu, yoksa şüphe dolduğunda gelen kaybetme ekranı mı
belirsiz. Kullanıcıya sorulmalı.
