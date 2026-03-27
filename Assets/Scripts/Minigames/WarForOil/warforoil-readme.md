# War For Oil — Minigame Sistemi

## Genel Bakis

Oyuncu, petrol kaynaklari zengin ulkeleri secip hukumetine baski yaparak savas baslatir. Savas surecinde gelen eventlere yanit vererek halk destegini yonetir. Savas sonunda olasilik tabanli bir kontrol yapilir — kazanilirsa ulkenin kaynaklari ele gecirilir, kaybedilirse agir cezalar uygulanir ve **minigame kalici olarak devre disi kalir**.

Savas sirasinda **event zincirleri** (hukumet fonlama krizi gibi), **rakip isgal** (baska bir ulkenin ayni hedefe saldirmasi), **toplum tepkisi** (savas karsiti gosteriler), **vandalizm** (pasif gelir urunlerine saldiri) ve **medya takibi** (savas karsiti gazetecilerin pesine dusmesi) tetiklenebilir.

---

## Mimari

Sistem 4 ScriptableObject + 1 Manager + yardimci siniflardan olusur:

```
WarForOilDatabase (SO)          — tum ayarlar, event havuzlari ve ulke havuzu
  ├── WarForOilCountry (SO)     — tek bir ulke verisi
  ├── WarForOilEvent (SO)       — savas sirasi event (normal / zincir / kose kapma / rakip teklif)
  │     └── WarForOilEventChoice (Serializable) — event secenegi
  ├── ChainRole (enum)          — event zincir rolu (None / Head)
  └── ChainBranch (Serializable) — zincir dallanma hedefi

WarForOilManager (MonoBehaviour, Singleton) — ana mantik
WarForOilResult (Serializable)              — savas sonucu verisi
WarForOilState (enum)                       — durum makinesi

WomanProcessDatabase (SO)                   — kadin sureci ayarlari ve event havuzlari
WomanProcessManager (MonoBehaviour, Singleton) — kadin sureci mantigi (bagimsiz)
```

Asset olusturma: `Assets → Create → Minigames → WarForOil → Database / Country / Event`

---

## Durum Makinesi

```
Idle ──→ PressurePhase ──→ WarProcess ←──→ EventPhase
              │                │
              ↓                ↓
            Idle          ResultPhase ──→ Idle
```

| Durum | Aciklama |
|-------|----------|
| **Idle** | Minigame bosta. Ulke rotasyonu devam eder. |
| **CountrySelection** | (Rezerve) Ilerde kullanilabilir. Su an Idle'dan dogrudan PressurePhase'e gecilir. |
| **PressurePhase** | Ulke secildi. Oyuncu "Baski Yap" butonuyla siyasi nufuza dayali basari kontrolu yapar. Basarisizsa cooldown. |
| **WarProcess** | Savas baslamis. Timer ilerler, belirli araliklarda eventler tetiklenir. |
| **EventPhase** | Savas sirasinda event geldi. Oyun duraklatilir, oyuncu karar verir veya sure dolar. |
| **ResultPhase** | Savas bitti, sonuc ekrani gosteriliyor. Oyun duraklatilmis. UI ekrani kapatinca stat'lar uygulanir. |

---

## Veri Siniflari

### WarForOilDatabase

Tum minigame ayarlarinin tek noktadan yonetildigi ScriptableObject.

| Alan | Varsayilan | Aciklama |
|------|-----------|----------|
| **Ulkeler** | | |
| `countries` | — | Tum ulke havuzu |
| **Ulke Rotasyonu** | | |
| `visibleCountryCount` | 3 | UI'da ayni anda goruntulenen ulke sayisi |
| `rotationInterval` | 90 sn | Ulke degisim araligi |
| **Baski Ayarlari** | | |
| `pressureCooldown` | 20 sn | Basarisiz baski sonrasi bekleme |
| `politicalInfluenceMultiplier` | 0.01 | Siyasi nufuzun basari sansina carpani |
| **Savas Eventleri** | | |
| `events` | — | Savas sirasinda tetiklenen normal event havuzu |
| **Zincir** | | |
| `chainDoubleChance` | 0.5 | 3'lu dongude 2 chain slotu cikma olasiligi (%50) |
| **Savas Ayarlari** | | |
| `warDuration` | 300 sn | Savas suresi (5 dakika) |
| `eventInterval` | 15 sn | Event kontrol araligi |
| `initialSupportStat` | 50 | Destek stat baslangic degeri |
| **Sonuc Ayarlari** | | |
| `baseWinChance` | 0.375 | Temel kazanma sansi |
| `supportWinBonus` | 0.625 | Tam destegin kazanma sansina max katkisi |
| `supportRewardRatio` | 0.8 | Support 100 olsa bile baseReward'in max bu orani alinir |
| `minWinChance` | 0.1 | Minimum kazanma sansi (%10) |
| `maxWinChance` | 0.9 | Maximum kazanma sansi (%90) |
| **Ateskes Ayarlari** | | |
| `ceasefireMinSupport` | 40 | Ateskes yapabilmek icin minimum destek degeri |
| `ceasefirePenalty` | 100 | En kotu ateskesteki para kaybi |
| `ceasefireMaxReward` | 200 | En iyi ateskesteki max kazanc carpani |
| **Odul/Ceza Ayarlari** | | |
| `warLossPenalty` | 200 | Kaybetme para cezasi |
| `warLossPoliticalPenalty` | 20 | Kaybetme siyasi nufuz dususu |
| `warLossSuspicionIncrease` | 15 | Kaybetme suphe artisi |
| **Rakip Isgal Ayarlari** | | |
| `rivalInvasionMinWarTime` | 60 sn | Rakip isgalin en erken tetiklenebilecegi savas suresi |
| `rivalInvasionChance` | 0.3 | Her event check'te rakip isgal tetiklenme sansi |
| `rivalDealRewardRatio` | 0.6 | Anlasma kabul edilince oyuncuya kalan odul orani |
| `rivalDealEndDelay` | 10 sn | Anlasma kabul edilince savas bitis gecikmesi |
| `initialCornerGrabStat` | 50 | Kose kapma stat baslangic degeri (0-100) |
| `rivalOfferEvent` | — | Rakip isgal teklif event'i (tek bir event, tum rakipler icin ortak) |
| `cornerGrabEvents` | — | Kose kapma yarisi event havuzu |
| **Toplum Tepkisi Ayarlari** | | |
| `protestMinWarTime` | 90 sn | Toplum tepkisinin en erken tetiklenebilecegi savas suresi |
| `protestChance` | 0.25 | Her event check'te toplum tepkisi tetiklenme sansi |
| `initialProtestStat` | 30 | Toplum tepkisi baslangic degeri (0-100) |
| `protestFailThreshold` | 80 | Bu degerin ustunde savas otomatik ateskese baglanir |
| `protestSuccessThreshold` | 10 | Bu degerin altina dusurulurse tepki bastirilmis sayilir |
| `protestDriftInterval` | 3 sn | Pasif drift tick araligi |
| `protestDriftDivisor` | 10 | drift = son choice modifier / divisor (her tick'te) |
| `protestTriggerEvent` | — | Toplum tepkisi baslangic event'i |
| `protestEvents` | — | Toplum tepkisi event havuzu |
| **Vandalizm Ayarlari** | | |
| `vandalismTriggerEvent` | — | Vandalizm baslangic event'i (trigger) |
| `vandalismDamageInterval` | 5 sn | Vandalizm hasar tick araligi |
| `vandalismLightDamage` | 5 | Light seviyede tick basina wealth kaybi |
| `vandalismModerateDamage` | 15 | Moderate seviyede tick basina wealth kaybi |
| `vandalismHeavyDamage` | 30 | Heavy seviyede tick basina wealth kaybi |
| `vandalismSevereDamage` | 50 | Severe seviyede tick basina wealth kaybi |
| **Medya Takibi Ayarlari** | | |
| `mediaPursuitMinWarTime` | 120 sn | Medya takibinin en erken tetiklenebilecegi savas suresi |
| `mediaPursuitChance` | 0.2 | Her event check'te medya takibi tetiklenme sansi |
| `mediaPursuitTriggerEvent` | — | Medya takibi baslangic event'i (trigger) |
| `initialMediaPursuitLevel` | Low | Otomatik tetiklemede baslangic seviyesi |
| `mediaPursuitLevel1Events` | — | Low state event havuzu |
| `mediaPursuitLevel2Events` | — | Medium state event havuzu |
| `mediaPursuitLevel3Events` | — | High state event havuzu |
| `mediaPursuitTickInterval` | 5 sn | Periyodik etki tick araligi |
| `mediaPursuitLowReputationPerTick` | 1 | Low seviyede tick basina itibar kaybi |
| `mediaPursuitLowSuspicionPerTick` | 0.5 | Low seviyede tick basina suphe artisi |
| `mediaPursuitMediumReputationPerTick` | 2 | Medium seviyede tick basina itibar kaybi |
| `mediaPursuitMediumSuspicionPerTick` | 1.5 | Medium seviyede tick basina suphe artisi |
| `mediaPursuitHighReputationPerTick` | 4 | High seviyede tick basina itibar kaybi |
| `mediaPursuitHighSuspicionPerTick` | 3 | High seviyede tick basina suphe artisi |

### WarForOilCountry

Her ulke icin ayri bir ScriptableObject asset'i olusturulur.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `displayName` | UI'da gorunecek ulke adi |
| `description` | Ulke aciklamasi (TextArea) |
| `baseReward` | Savas kazanildiginda taban odul |
| `invasionDifficulty` | 0-1 arasi, isgal zorlugu → kazanma sansini dusurur |

**Not:** Eventler ulke uzerinde degil, Database uzerinde tanimlanir. Tum ulkeler ayni event havuzunu paylasir.

### WTETWCEventGroup

Event grup ve iliski sistemi. Ayni konseptteki eventler arasinda agirlik ve karsilikli dislama kurallari tanimlanir. Database'deki `eventGroups` listesine eklenir.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `description` | Grup aciklamasi (TextArea) |
| `members` | Grup uyeleri listesi (EventGroupMember) |
| `maxTriggerCount` | Ayni savasta en fazla kac event tetiklenebilir (-1 = sinirsiz) |

**EventGroupMember:**

| Alan | Aciklama |
|------|----------|
| `warEvent` | Grup uyesi event referansi |
| `weightLevel` | Tetiklenme agirligi enum'u (ExtremelyLess=0.25x, Less=0.5x, Normal=1x, More=1.25x, Extreme=1.5x) |

**Calisma Mantigi:**
- **Agirlik:** Event seciminde weighted random kullanilir. Normal disindaki seviyeler event'in secilme sansini orantili olarak degistirir. Ornegin 100 eventlik havuzda her biri %1 sansla gelir; ExtremelyLess secilirse o event %0.25'e duser, aradaki fark diger eventlere dagilir.
- **Karsilikli Dislama:** maxTriggerCount=2 ve grupta 3 event varsa, 2 tanesi tetiklendikten sonra 3. su havuzdan cikarilir.
- Grup disindaki eventler Normal (1.0x) ile calisir.

### OFPCEventGroup

Oil for Peace Concept event grup sistemi. Bir choice'ta `blocksEventGroup` secildiginde bu gruptaki tum eventler o savas boyunca bir daha tetiklenmez. Database'deki `ofpcEventGroups` listesine eklenir.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `description` | Grup aciklamasi (TextArea) |
| `members` | Grup uyesi event referanslari (List\<WarForOilEvent\>) |

### WarForOilEvent

Savas sirasinda tetiklenen karar olaylari. Ayni event sinifi normal eventler, zincir eventleri, kose kapma eventleri ve rakip teklif event'i icin kullanilir. Eventin hangi havuza ait oldugu Database'deki listeye gore belirlenir.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `displayName` | Event basligi |
| `description` | Event aciklamasi (TextArea) |
| `devNote` | Sadece Inspector'da gorunen gelistirici notu (oyuna etkisi yok) |
| `conditionalDescriptions` | Hikaye bayragi aktifse default aciklama yerine alternatif metin gosterilir. Ilk eslesen bayrak gecerli olur. |
| `minWarTime` | Savas suresinin yuzdesi olarak en erken tetiklenme (0-1, orn. 0.2 = %20, 300sn savasta 60sn) |
| `maxWarTime` | Savas suresinin yuzdesi olarak en gec tetiklenme (-1 = sinirsiz, 0-1 arasi yuzde) |
| `decisionTime` | Karar suresi (varsayilan 10 sn) |
| `isRepeatable` | Ayni savasta tekrar tetiklenebilir mi |
| `isUnlimitedRepeat` | Sinirsiz tekrar (isRepeatable true ise, maxRepeatCount yok sayilir) |
| `maxRepeatCount` | En fazla kac kez tekrar edebilir (isRepeatable true ve isUnlimitedRepeat false ise) |
| `choices` | Secenek listesi |
| `defaultChoiceIndex` | Sure dolunca secilecek secenek (-1 = ilk available secenek) |
| **Zincir Ayarlari** | |
| `chainRole` | None / Head (Head = zincir baslatici, dallanma choice seviyesinde tanimlanir) |
| `blocksSubChainBranching` | true ise bu event tetiklendikten sonra baska zincirlerde dallanma hedefi olarak secilemez. Alt agaci da dolayisiyla erisilemez hale gelir (altinda Head varsa o bagimsiz tetiklenebilir). |
| `alsoBlockedBranchEvents` | blocksSubChainBranching tetiklenince bu listedeki event'ler de dallanma hedefi olarak engellenir (kendileri tetiklenmemis olsa bile). |
| **Vandalizm Tetikleme** | |
| `isVandalismEvent` | Bu event tetiklendiginde vandalizm seviyesi otomatik degisir |
| `vandalismLevelOnTrigger` | Tetiklendiginde atanacak vandalizm seviyesi |
| **Medya Takibi Tetikleme** | |
| `isMediaPursuitEvent` | Bu event tetiklendiginde medya takibi seviyesi otomatik degisir |
| `mediaPursuitLevelOnTrigger` | Tetiklendiginde atanacak medya takibi seviyesi |
| **Kadin Sureci** | |
| `isWomanProcessEvent` | true ise bu event kadin sureci havuzlarinda kullanilir. Choice'larda womanObsessionModifier alani gorunur. |
| `minObsession` / `maxObsession` | Eventin havuzdan secilebilecegi obsesyon araligi. Tier araligini daraltabilir ama genisletemez (kesisim alinir). Kesisim yoksa ozel aralik gecersiz sayilir, tier araligi kullanilir. Sadece havuzdan rastgele secimde gecerli — zincir dallanmasi eventlerinde bu filtre uygulanmaz. |
| `blockedWomanProcessEvents` | Bu event tetiklenince listedeki eventler havuzdan ve zincirlerden cikarilir. Head ise zinciri hic baslamaz, dal ise agirligi digerlerine kayar. |
| **Oncu Event** | |
| `hasPrecursorEvent` | true ise bu kadin eventinin bir oncu eventi vardir. Kadin eventi gelmeden once oncu event tetiklenir, 4 saniye sonra asil kadin eventi gelir. |
| `precursorEventType` | Oncu eventin tipi: `WarForOil` veya `RandomEvent`. WarForOil secilirse savas yokken bu kadin eventi ve oncusu ikisi de tetiklenmez. |
| `precursorWarEvent` | Oncu war for oil eventi (precursorEventType=WarForOil ise). |
| `precursorRandomEvent` | Oncu random event (precursorEventType=RandomEvent ise). |

#### ChainRole Enum

| Deger | Aciklama |
|-------|----------|
| `None` | Normal event, zincir disi |
| `Head` | Zincirin baslangic event'i — normal havuzdan tetiklenir, chain surecini baslatir |

Inspector'da sadece None/Head secimi yapilir. Dallanma (hangi event'e gidilecegi) choice seviyesinde `chainBranches` ile belirlenir.

### WarForOilEventChoice

Event icindeki tek bir secenek. Serializable sinif.

| Alan | Aciklama |
|------|----------|
| **Temel Alanlar** | |
| `displayName` | Secenek adi |
| `description` | Secenek aciklamasi (TextArea) |
| `supportModifier` | Destek stat degisimi (+ = destek artar) |
| `suspicionModifier` | Suphe degisimi |
| `reputationModifier` | Itibar degisimi (+ = artar, - = duser) |
| `politicalInfluenceModifier` | Politik nufuz degisimi (- = dusurur) |
| `costModifier` | Maliyet degisimi (int, savas sonunda birikimli uygulanir) |
| `wealthModifier` | Anlik para degisimi (+ = kazan, - = kaybet, secildiginde hemen uygulanir) |
| `cornerGrabModifier` | Kose kapma stat degisimi (+ = bizim lehimize, sadece yaris aktifken uygulanir) |
| **Protest Etkisi** (foldout) | |
| `protestModifier` | Toplum tepkisi stat degisimi (+ = tepki artar, - = azalir, sadece tepki aktifken uygulanir). Ayrica sonraki drift hizini belirler. |
| `protestTriggerChanceBonus` | Protest tetiklenme sansina eklenen bonus (0-1). Her basarisiz check'te yarilarak soner. |
| `hasProtestChance` | true ise protestModifier yerine olasilik bazli sistem kullanilir |
| `protestDecreaseChance` | Azalma ihtimali (0-1, orn. 0.6 = %60) |
| `protestDecreaseAmount` | Azalma miktari (pozitif deger, otomatik cikarilir) |
| `protestIncreaseAmount` | Artma miktari (pozitif deger, otomatik eklenir) |
| **Diger Sonuclar** (foldout) | |
| `endsWar` | Bu secenek savasi bitirir mi |
| `warEndDelay` | Savas kac saniye sonra biter (0 = aninda) |
| `reducesReward` | Odulu dusurur mu |
| `baseRewardReduction` | Base reward'i bu oranda dusurur (0.3 = %30 dusus) |
| `endsWarWithDeal` | Savasi anlasmayla bitirir (garanti odul) |
| `dealDelay` | Anlasma kac saniye sonra savasi bitirir |
| `dealRewardRatio` | Normal kazanimin bu orani garanti verilir (0.8 = %80) |
| `blocksEvents` | Secilirse savas sonuna kadar yeni event gelmez |
| `eventBlockCycles` | Gecici event engeli — bu kadar event donemi boyunca event gelmez (0-10, 0=etkisiz) |
| `blocksCeasefire` | Secilirse savas sonuna kadar oyuncunun ateskes butonu engellenir |
| `blocksEventGroup` | Secilirse bu event'in ait oldugu gruptaki (OFPC/WTETWC) tum eventler bir daha tetiklenmez |
| `setsStoryFlags` | Bu choice secildiginde aktif edilen hikaye bayraklari (List&lt;StoryFlag&gt;). Kalici — bir kez aktif edildikten sonra oyun boyunca gecerli. |
| `hasImmediateEvent` | Secildiginde hic beklemeden havuzdan rastgele bir event tetiklenir (WarProcess'e donmeden) |
| `immediateEventPool` | Agirlikli event havuzu (List&lt;ImmediateEventEntry&gt;). Her giris: targetEvent + weight. Agirliga gore rastgele secilir |
| `hasProbabilisticWarEnd` | Olasilik bazli savas bitirme (3 sonuc: savas biter / event yok olur / tekrar tetiklenir) |
| `probWarEndChance` | Savas bitme olasiligi (0-1, support=50 icin base deger) |
| `probDismissChance` | Event yok olma olasiligi (0-1, support=50 icin base deger). Tekrar tetiklenme = 1 - warEnd - dismiss |
| `probWarEndDelay` | Savas biterse gecikme suresi (saniye, 0 = aninda) |
| `hasProbabilisticRewardReduction` | Olasilik bazli odul dusurme (3 sonuc: event tekrar tetiklenir / odul duser / hicbir sey olmaz) |
| `probRetriggerChance` | Event tekrar tetiklenme sansi (0-1) |
| `probRewardReductionChance` | Odul dusme sansi (0-1). Hicbir sey olmama = 1 - retrigger - reduction |
| `probRewardReductionAmount` | Odul dusme miktari (0.3 = %30, rewardMultiplier'a carpimsal uygulanir) |
| **Feed Sonuclari** (foldout) | |
| `freezesFeed` | Secilince sosyal medya feed'ini dondurur (SocialMediaManager.TryFreezeFeed) |
| `slowsFeed` | Secilince sosyal medya feed'ini yavaslatir (SocialMediaManager.TrySlowFeed) |
| `hasFeedOverride` | Feed'i belirli bir konuya yonlendirir (SocialMediaManager.SetEventOverride) |
| `feedOverrideTopic` | Yonlendirilecek konu (TopicType enum) |
| `feedOverrideRatio` | Yonlendirme orani (0-1, orn. 0.8 = %80) |
| `hasCounterFeedTopic` | 2. konu ekler — istenmeyen konulari bastirmak icin |
| `counterFeedTopic` | Counter konu (TopicType enum) |
| `counterFeedRatio` | Counter konu orani (0-1) |
| `feedOverrideDuration` | Yonlendirme suresi (saniye, her iki topic icin ortak) |
| **Zincir Dallanmasi** (foldout) | |
| `chainInfluenceStat` | Dallanma secimini etkileyen stat (ChainInfluenceStat: JustLuck / Wealth / Suspicion / Reputation / PoliticalInfluence) |
| `chainThreshold0/1/2` | Stat aralik esikleri (varsayilan 20/50/75, 4 aralik olusturur) |
| `chainBranches` | Dallanma hedefleri listesi (List\<ChainBranch\>). Bos = chain biter. Dolu = sonraki chain event bu listeden secilir. Her branch'te 4 aralik agirligi (weightRange0-3). |
| **Zincir Sayac** | |
| `incrementsChainCounter` | Bu choice secildiginde zincir sayacini artirir |
| `chainCounterKey` | Sayac adi (serbest string, orn. "acele", "yavasla") |
| `chainCounterIncrement` | Artis miktari (varsayilan 1) |
| `hasEarlyChainTrigger` | Sayac esige ulasirsa zinciri atlayip direkt hedef event'e gecer |
| `earlyTriggerThreshold` | Erken tetikleme esik degeri |
| `earlyTriggerEvent` | Esik asildikca tetiklenecek event |
| **Rakip Isgal Flagleri** (foldout) | |
| `acceptsRivalDeal` | Rakip isgal anlasmasini kabul eder |
| `rejectsRivalDeal` | Rakip isgal anlasmasini reddeder → kose kapma yarisi baslar |
| **Vandalizm Etkisi** (foldout) | |
| `affectsVandalism` | Bu choice vandalizm seviyesini degistirir mi |
| `vandalismChangeType` | Direct (hedef seviye ata) veya Relative (+/- tik kaydirma) |
| `vandalismTargetLevel` | Direct modda: hedef VandalismLevel (None/Light/Moderate/Heavy/Severe/Ended) |
| `vandalismLevelDelta` | Relative modda: seviye degisimi (orn. +2 = 2 tik artir, -1 = 1 tik azalt) |
| **Medya Takibi Etkisi** (foldout) | |
| `affectsMediaPursuit` | Bu choice medya takibi seviyesini degistirir mi |
| `mediaPursuitChangeType` | Direct (hedef seviye ata) veya Relative (+/- tik kaydirma) |
| `mediaPursuitTargetLevel` | Direct modda: hedef MediaPursuitLevel (None/Low/Medium/High/Ended) |
| `mediaPursuitLevelDelta` | Relative modda: seviye degisimi (orn. +1 = 1 tik artir, -1 = 1 tik azalt) |
| **Kadin Sureci** | |
| `startsWomanProcess` | Secildiginde kadin surecini baslatir (oyun boyunca tek sefer, sadece savas icinde calisir) |
| `endsWomanProcess` | Secildiginde kadin surecini aninda bitirir (obsesyon degeri ne olursa olsun) |
| `womanObsessionModifier` | Kadin sureci stat degisimi (+ = obsesyon artar, - = azalir). Sadece isWomanProcessEvent acikken Inspector'da gorunur. |
| `redirectsWomanPool` | true ise secildiginde kadin sureci havuzunu kalici olarak baska bir WomanProcessDatabase'e yonlendirir. Sadece isWomanProcessEvent acikken Inspector'da gorunur. |
| `womanPoolDatabase` | Yonlendirilecek WomanProcessDatabase referansi. redirectsWomanPool aktifken gorunur. |
| `freezesWomanProcess` | true ise kadin surecini belirli dongu sayisi kadar dondurur. Sadece isWomanProcessEvent acikken gorunur. |
| `womanProcessFreezeCycles` | Kac dongu boyunca kadin eventi tetiklenmeyecek (min 1). freezesWomanProcess aktifken gorunur. Mevcut dondurma varsa ustune eklenir. |
| `hasObsessionDropLimit` | true ise bu choice secildikten sonra obsesyon zirve degerinden belirli miktar duserse kadin sureci otomatik sona erer. Zirve takipli: obsesyon yukselirse esik de yukselir. Low→Mid kademe gecisinde limit kalici olarak kalkar. |
| `obsessionDropLimit` | Zirve obsesyondan bu kadar duserse surec biter. Ornek: 12'de secildi, limit 3 → esik 9. Obsesyon 15'e cikti → esik 12. Birden fazla limit varsa en siki (kucuk delta) gecerli olur. |
| **Kalici Stat Carpanlari** (foldout) | |
| `permanentMultipliers` | Liste: birden fazla stat icin kalici carpan tanimlanabilir. Her entry: `stat` (PermanentMultiplierStatType) + `multiplier` (float). Ornek: 1.1 = %10 artis. Carpisimsal birikir. Tum oyun boyunca, tum kaynaklardan gecerlidir. Secenekler: Wealth, Suspicion, Reputation, PoliticalInfluence, WarSupport, WomanObsession. WarSupport icin WarForOilManager, WomanObsession icin WomanProcessManager, digerleri icin GameStatManager uygulanir. |
| **On Kosullar** (foldout) | |
| `requiredSkills` | Bu secenek icin acilmis olmasi gereken skill'ler |
| `statConditions` | Bu secenek icin saglanmasi gereken stat kosullari |

### WarForOilResult

Savas sonucu. Manager tarafindan olusturulur, event'lerle UI'a iletilir.

| Alan | Aciklama |
|------|----------|
| `country` | Savas yapilan ulke |
| `warWon` | Kazanildi mi |
| `wasCeasefire` | Ateskes mi yapildi |
| `wasDeal` | Anlasmayla mi bitti |
| `wasCornerGrabRace` | Kose kapma yarisi miydi |
| `wasProtestCeasefire` | Toplum tepkisi yuzunden ateskes mi |
| `rivalCountry` | Rakip ulke (varsa) |
| `rivalRewardGain` | Rakip ulkenin kazandigi bonus reward |
| `finalSupportStat` | Savas sonu destek degeri |
| `finalProtestStat` | Toplum tepkisi son degeri (aktifse) |
| `finalVandalismLevel` | Savas sonu vandalizm seviyesi (VandalismLevel enum) |
| `winChance` | Hesaplanan kazanma sansi |
| `wealthChange` | Para degisimi (+ kazanc, - kayip) |
| `suspicionChange` | Suphe degisimi |
| `reputationChange` | Itibar degisimi |
| `politicalInfluenceChange` | Siyasi nufuz degisimi |

---

## Event Zinciri Sistemi

Bazi eventler dallanmali zincirler olusturur. Zincir, Head event ile baslar ve her choice'un `chainBranches` listesi ile dallanir. Normal event dongusune entegre calisir (ayri state yok).

### ChainInfluenceStat (choice seviyesinde)

Dallanma secimini etkileyen stat. Choice'un `chainInfluenceStat` alaninda secilir, tum branch'ler ayni stat'i paylasir.

| Deger | Aciklama |
|-------|----------|
| `JustLuck` | Stat etkisi yok, sadece weightRange0 kullanilir (saf sans) |
| `Wealth` | Para stat'ina gore |
| `Suspicion` | Suphe stat'ina gore |
| `Reputation` | Itibar stat'ina gore |
| `PoliticalInfluence` | Politik nufuz stat'ina gore |

**Esik degerleri** (choice seviyesinde): `chainThreshold0` (varsayilan 20), `chainThreshold1` (varsayilan 50), `chainThreshold2` (varsayilan 75)
3 esik ile 4 aralik olusur: 0-t0, t0-t1, t1-t2, t2-100

### ChainBranch

Bir choice secildiginde siradaki chain event'in olasi hedeflerinden birini tanimlar.

| Alan | Aciklama |
|------|----------|
| `targetEvent` | Dallanmanin hedef event'i (WarForOilEvent referansi) |
| `weightRange0` | Aralik 0 agirligi (JustLuck'ta tek agirlik) |
| `weightRange1` | Aralik 1 agirligi |
| `weightRange2` | Aralik 2 agirligi |
| `weightRange3` | Aralik 3 agirligi |
| `triggersAsImmediateEvent` | true ise secilen branch zincir devami yerine aninda event olarak tetiklenir. Zincir biter, event standalone olarak gosterilir. |
| `immediateEventDelay` | Aninda event gecikmesi (0-10 saniye). triggersAsImmediateEvent aktifken gorunur. 0 = aninda, N = N saniye sonra tetiklenir. |

**Kosullu dallanma (choice seviyesinde):**
- `hasConditionalBranching`: true ise kosullu dallanma aktif
- `branchCounterKey`: Sayac adi
- `branchCounterMin`: Minimum sayac degeri (dahil)
- `branchCounterMax`: Maksimum sayac degeri (-1 = sinirsiz)

Kosul saglanirsa `conditionalChainBranches` listesinden, saglanmazsa `chainBranches` listesinden secim yapilir. Iki havuzun agirliklari bagimsizdir.

**Secim mantigi:**
1. Stat'in mevcut yuzdesi (0-100) hesaplanir
2. Esik degerlerine gore hangi aralikta oldugu belirlenir
3. O araligin agirligi (weightRangeX) kullanilir
4. Kosullu dallanma aktifse: kosul kontrol edilir, gecen havuz secilir
5. Secilen havuzdaki branch'lerin agirliklarindan normalize edilip agirlikli random secim yapilir
6. JustLuck modunda aralik yok, sadece weightRange0 kullanilir

**Not:** Her aralik icin tum branch'lerin agirlik toplami ~1 olmalidir (olasilik gibi girilir).

### Inspector'da Zincir Kurulumu

1. Baslangic event'inin `chainRole`'unu **Head** yap
2. Head event'in choice'larinda "Zincir Dallanmasi" foldout'unu ac
3. **Etkileyen Stat** sec (JustLuck veya bir GameStat)
4. Stat secildiyse **3 esik degerini** ayarla (varsayilan 20/50/75)
5. `+ Dal Ekle` ile dallanma hedeflerini ekle (targetEvent + aralik basi agirlik)
6. Hedef event'lerin choice'larinda da `chainBranches` doldur (dallanma devam etsin)
7. Bir choice'un `chainBranches` listesi **bos** birakilirsa chain o noktada dogal olarak biter

### 3'lu Event Dongusu (chain aktifken)

Chain aktifken normal event dongusu 3'lu cycle'lara bolunur:

1. **Dongu basinda** (counter=0): `chainSlotsRemaining = Random < chainDoubleChance ? 2 : 1`
2. **Her slot icin** sampling without replacement:
   - `slotsLeft = 3 - chainCycleCounter`
   - Chain olasiligi = `chainSlotsRemaining / slotsLeft`
   - Random < olasilik → **chain slotu** (pendingChainBranches'tan agirlikli secim)
   - Degilse → **random slotu** (normal TryTriggerWarEvent, Head eventler haric)
3. Counter 3'e ulasinca sifirla, yeni dongu baslat
4. `eventBlockCycles` sadece random slotlari etkiler, chain slotlarini etkilemez

### Zincir Akisi

1. Normal havuzdan Head event tetiklenir → `isInChain = true`
2. Oyuncu choice secer → choice'un `chainBranches` → `pendingChainBranches`
3. Sonraki chain slotunda `pendingChainBranches`'tan biri secilir (stat bazli veya sans bazli) → tetiklenir
4. Tekrar choice secilir → `chainBranches` → yeni `pendingChainBranches`
5. `chainBranches` bos olan choice secilirse → `isInChain = false`, chain dogal olarak biter
6. Savas biterse (timer, endsWar, ceasefire, protest) → chain de biter, ozel ceza yok

### Zincir Sirasinda Diger Sistemler

Eski sistemden farkli olarak, chain aktifken diger sistemler **engellenmez**:
- Rakip isgal tetiklenebilir
- Toplum tepkisi tetiklenebilir
- Medya takibi tetiklenebilir
- EventCoordinator lock/unlock kullanilmaz

---

## Rakip Isgal ve Kose Kapma Sistemi

Savas sirasinda baska bir ulke ayni hedef ulkeye saldirabilir. Bu durumda oyuncuya anlasma teklifi yapilir.

### Tetiklenme Kosullari

- `warTimer >= rivalInvasionMinWarTime` (varsayilan 60 sn)
- Bu savasta henuz rakip isgal tetiklenmemis (`rivalInvasionTriggered == false`)
- Kose kapma yarisi aktif degil
- Her event check'te `rivalInvasionChance` (varsayilan %30) ihtimalle tetiklenir

### Rakip Ulke Secimi

`database.countries` listesinden rastgele secilir. Su an isgal edilen ulke (`selectedCountry`) ve daha once fethedilen ulkeler (`conqueredCountries`) haric tutulur. Havuzda uygun ulke yoksa rakip isgal tetiklenmez.

### Anlasma Teklifi

Database'deki tek `rivalOfferEvent` event'i gosterilir. Choice'lardaki flagler:
- `acceptsRivalDeal` → anlasmayi kabul et
- `rejectsRivalDeal` → anlasmayi reddet

### Anlasma Kabul Edilirse

1. `eventsBlocked = true` → savas sonuna kadar **hicbir event gelmez**
2. Savas suresi `warDuration - rivalDealEndDelay` kadar ileri sarilir (hizli bitis)
3. Garanti odul: `baseReward * rivalDealRewardRatio` (varsayilan %60 oyuncuya)
4. Rakip ulkenin payi (`baseReward * (1 - rivalDealRewardRatio)`) rakip ulkenin `bonusRewards`'una eklenir
5. Ileride o ulke isgal edilirse bonus reward da alinir

### Anlasma Reddedilirse → Kose Kapma Yarisi

1. `isCornerGrabRace = true` olur
2. `cornerGrabStat` baslangic degerine set edilir (varsayilan 50)
3. Event havuzu `database.cornerGrabEvents`'e gecis yapar (normal eventler yerine)
4. Event trigger sayaclari sifirlanir (yeni havuz icin)
5. Oyuncunun event secimleri `cornerGrabModifier` ile cornerGrabStat'i etkiler

### Kose Kapma Stat (0-100)

- 100 = tamamen bizim lehimize → tum base reward bize
- 0 = tamamen rakip lehine → tum base reward rakibe
- 50 = esit paylasim

Savas sonunda (kazanildiysa):
```
grabRatio = cornerGrabStat / 100
oyuncuOdulu = baseReward * grabRatio * supportRewardRatio
rakipPayi = baseReward * (1 - grabRatio) → bonusRewards'a eklenir
```

### bonusRewards (Kalici)

Rakip isgallerden ulkelere eklenen bonus odulller `Dictionary<WarForOilCountry, float>` olarak saklanir. Savaslar arasi **kalici**dir — sifirlanmaz. Bir ulke isgal edildiginde `GetEffectiveBaseReward()` ile baseReward + bonus hesaplanir.

---

## Toplum Tepkisi Sistemi

Savas sirasinda "savas karsiti gosteriler basladi" seklinde bir toplum tepkisi tetiklenebilir. Oyuncunun gorevi tepkiyi dizginlemek — basaramazsa savas otomatik ateskese baglanir.

### Iki Asamali Tetikleme

**Faz 1 — Foreshadowing:**
- Event check zamani gelince sistem toplum tepkisi icin zar atar
- Basarili olursa `protestPending = true` set edilir
- Oyuncuya event **gosterilmez** — sadece `OnProtestForeshadow` event'i tetiklenir
- UI bu event'i dinleyerek feed'i savas karsiti gonderilere cevirebilir
- Bu cycle tuketilir, normal event secimi yapilmaz

**Faz 2 — Aktivasyon:**
- Bir sonraki event check'te `protestPending` kontrol edilir
- `protestTriggerEvent` oyuncuya gosterilir (baslangic event'i)
- `protestStat` baslangic degerine set edilir (varsayilan 30)
- Toplum tepkisi aktif olur

### Tetiklenme Kosullari

- `warTimer >= protestMinWarTime` (varsayilan 90 sn)
- Bu savasta henuz toplum tepkisi tetiklenmemis (`protestTriggered == false`)
- Zincir aktif degil
- Her event check'te `protestChance` (varsayilan %25) ihtimalle tetiklenir

### Cift Havuzlu Event Sistemi

Kose kapmadan farkli olarak havuz **degismez**, ek havuz devreye girer:
- Aktif havuz (normal veya cornerGrab) calismaya devam eder
- Ek olarak `database.protestEvents` havuzundan da eventler gelir
- Her event check'te her iki havuzdan uygun eventler birlestirilir, rastgele biri secilir

### Pasif Drift

Son secilen choice'un `protestModifier`'ina gore stat pasif olarak kayar:
- Her `protestDriftInterval` (varsayilan 3 sn) saniyede bir drift uygulanir
- `driftPerTick = lastProtestModifier / protestDriftDivisor`
- Ornek: choice +3 verdiyse → her 3sn'de +0.3 eklenir
- Ornek: choice -5 verdiyse → her 3sn'de -0.5 cikarilir
- Ilk choice'a kadar drift 0'dir (pasif degisim yok)

### Basarisizlik — protestStat >= protestFailThreshold (80)

- Savas otomatik ateskese baglanir (`ProtestForceCeasefire`)
- Mevcut ateskes formulune gore odul/ceza hesaplanir
- `wasProtestCeasefire = true` olarak isaretlenir

### Basari — protestStat < protestSuccessThreshold (10)

- Toplum tepkisi havuzundan event gelmesi durur
- Bu savas boyunca toplum tepkisi bir daha tetiklenmez
- Pasif drift durur
- Normal savas akisina devam edilir

### Choice Mekanikleri

Toplum tepkisi eventlerindeki choice'lar:
- **Feed dondurma**: Feed freeze skill'i acilmissa secilebilir (requiredSkills ile kontrol)
- **Mafya ile oldurme**: Ilgili mafya skill'i acilmissa secilebilir (requiredSkills ile kontrol)
- **Gosterilere izin verme**: Genel secenek (herkes secebilir)
- Her choice'un `protestModifier`'i var — hem aninda uygulanir hem sonraki drift hizini belirler

---

## Vandalizm Sistemi

Toplum tepkisi eventleri sirasinda vandallar oyuncunun pasif gelir urunlerine saldirabilir. Vandalizm surekli bir stat degil, kesikli seviye (enum) olarak yonetilir.

### Vandalizm Tetikleme (Trigger)

Vandalizm protest gibi trigger event mekanizmasi kullanir:
1. Bir protest event choice'i vandalizmi baslatacaksa (None → aktif seviye), direkt baslamaz
2. `vandalismPending = true` yapilir, hedef seviye saklanir
3. Bir sonraki 15 saniyelik event check'te `vandalismTriggerEvent` gosterilir
4. Trigger event gosterildiginde vandalizm aktif olur ve periyodik hasar baslar

Database'de `vandalismTriggerEvent` slotuna atanan event "Vandallar sokaklara cikti" gibi bir baslangic event'i olmali.

**Not**: Vandalizm zaten aktifse (None degilse) choice'lar direkt seviye degistirir, trigger tekrar gosterilmez.

### VandalismLevel Enum

| Seviye | Numerik | Aciklama |
|--------|---------|----------|
| `None` | — | Vandalizm baslamadi |
| `Light` | 1 | Hafif hasar |
| `Moderate` | 2 | Orta hasar |
| `Heavy` | 3 | Agir hasar |
| `Severe` | 4 | Siddetli hasar |
| `Ended` | — | Vandalizm bastirildi/bitti |

### Seviye Degisimi

Iki mod vardir (choice basina biri secilir):

**Direct**: Seviyeyi dogrudan hedef enum'a atar.
- Ornek: `vandalismTargetLevel = Heavy` → vandalizm aninda Heavy olur

**Relative**: Mevcut seviyeyi +/- tik kaydirır.
- Light=1, Moderate=2, Heavy=3, Severe=4
- Ornek: Light'tayken +2 → Heavy'ye gecer
- Sonuc < 1 → Ended (vandalizm bastirildi)
- Sonuc > 4 → Severe'de kalir (tavan)

**Koruma kurallari**:
- Vandalizm None iken Direct None/Ended → hicbir sey yapmaz
- Vandalizm None iken Relative negatif/sifir → hicbir sey yapmaz
- Vandalizm None iken aktif seviyeye gecis → trigger event bekletilir

### Periyodik Hasar

Aktif vandalizm seviyelerinde (Light-Severe) her `vandalismDamageInterval` saniyede bir wealth kaybi uygulanir:

| Seviye | Tick Basina Kayip |
|--------|------------------|
| Light | `vandalismLightDamage` (5) |
| Moderate | `vandalismModerateDamage` (15) |
| Heavy | `vandalismHeavyDamage` (30) |
| Severe | `vandalismSevereDamage` (50) |

None ve Ended seviyelerinde hasar uygulanmaz.

### Inspector Kullanimi

Choice'un "Vandalizm Etkisi" foldout'u acilir:
1. `Vandalizmi Etkiler` tiklanir
2. `Degisim Tipi` secilir (Direct veya Relative)
3. Direct ise `Hedef Seviye` secilir, Relative ise `Seviye Degisimi (+/-)` girilir

### Savas Basi / Sonu

- Savas basladiginda `currentVandalismLevel = None`, `vandalismPending = false`, hasar timer sifirlanir
- Savas sonucunda `finalVandalismLevel` kaydedilir (UI gosterebilir)

### Eventler

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| `OnVandalismLevelChanged` | VandalismLevel | Vandalizm seviyesi degisti |
| `OnVandalismDamage` | float | Hasar tick'i uygulandı (miktar) |

---

## Medya Takibi Sistemi

Savas sirasinda savas karsiti gazeteciler oyuncunun pesine dusebilir. Protest'ten bagimsiz olarak sans bazli tetiklenir. 3 aktif seviye (Low/Medium/High) + Ended state var. Her seviyenin kendi event havuzu ve farkli itibar/suphe etkisi vardir. Oyunu direkt bitiren bir state yok — suphe artisi oyunun kendi game over mekanizmasini tetikler (suspicion >= 100).

### MediaPursuitLevel Enum

| Deger | Numerik | Aciklama |
|-------|---------|----------|
| `None` | — | Sistem baslamadi |
| `Low` | 1 | Dusuk baski |
| `Medium` | 2 | Orta baski |
| `High` | 3 | Yuksek baski |
| `Ended` | — | Gazetecilerden kurtulundu |

### Tetikleme

Protest'ten tamamen bagimsiz, kendi `mediaPursuitMinWarTime` (120 sn) ve `mediaPursuitChance` (0.2) degerleri ile tetiklenir.
- Her event check'te sans kontrolu yapilir
- Foreshadow yok — direkt `mediaPursuitPending = true` → sonraki cycle `mediaPursuitTriggerEvent` gosterilir
- `mediaPursuitTriggered` flag ile savas basina 1 kez tetiklenir

### State Bazli Event Havuzu

Database'de 3 ayri liste:
- `mediaPursuitLevel1Events` — Low state'deyken gelen eventler
- `mediaPursuitLevel2Events` — Medium state'deyken gelen eventler
- `mediaPursuitLevel3Events` — High state'deyken gelen eventler

Sadece aktif state'in havuzu event pool'a eklenir. State degisince havuz otomatik degisir.

### State Gecisleri

Vandalizm ile ayni pattern. Iki yolla degisir:

**1. Choice bazli** (`affectsMediaPursuit`):
- `MediaPursuitChangeType.Direct`: Hedef seviye direkt atanir
- `MediaPursuitChangeType.Relative`: Mevcut seviye +/- tik kaydirilir (Low=1, Medium=2, High=3)
- None'dan aktif seviyeye geciste trigger event bekletilir

**2. Event bazli** (`isMediaPursuitEvent`):
- Event tetiklendiginde (choice'tan once) seviye otomatik degisir
- Choice seviyeyi tekrar degistirebilir (gayet normal)

### Koruma Kurallari

- Medya takibi None iken Direct None/Ended → hicbir sey yapmaz
- Medya takibi None iken Relative negatif/sifir → hicbir sey yapmaz
- Medya takibi None iken aktif seviyeye gecis → trigger event bekletilir

### Periyodik Etki (State bazli tick)

Her `mediaPursuitTickInterval` (5 sn) saniyede aktif state'e gore:
- Itibar kaybi: `GameStatManager.AddReputation(-reputationPerTick)`
- Suphe artisi: `GameStatManager.AddSuspicion(suspicionPerTick)`

| Seviye | Itibar Kaybi/Tick | Suphe Artisi/Tick |
|--------|-------------------|-------------------|
| Low | 1 | 0.5 |
| Medium | 2 | 1.5 |
| High | 4 | 3 |

Suphe 100'e ulasirsa GameStatManager kendi game over'ini tetikler — ek kontrol gerekmez.

### Bitmesi

- Choice ile `Ended`'a cekilir (Direct veya Relative alt sinir)
- Savas bitince otomatik biter (StartWar reset)
- Protest bastirilsa bile **bitmez** (bagimsiz sistem)

### Inspector'dan Ayarlama

**Event seviyesinde:**
1. Inspector'da `Medya Takibi Eventi` tiklenir
2. `Tetiklenince Seviye` dropdown'dan hedef seviye secilir

**Choice seviyesinde:**
1. `Medya Takibi Etkisi` foldout'u acilir
2. `Medya Takibini Etkiler` tiklenir
3. `Degisim Tipi`: Direct → `Hedef Seviye` dropdown, Relative → `Seviye Degisimi (+/-)` int

### Savas Basi / Sonu

- Savas basladiginda `currentMediaPursuitLevel = None`, `mediaPursuitPending = false`, tick timer sifirlanir
- Savas sonucunda `finalMediaPursuitLevel` kaydedilir (UI gosterebilir)

### Eventler

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| `OnMediaPursuitLevelChanged` | MediaPursuitLevel | Medya takibi seviyesi degisti |
| `OnMediaPursuitTick` | float, float | Medya takibi tick'i (reputationLoss, suspicionGain) |

---

## Formuller

### Baski Basari Sansi

```
successChance = clamp(politicalInfluence * politicalInfluenceMultiplier, 0, 0.95)
```

- Siyasi nufuz 0 veya negatifse → %0 sans
- Siyasi nufuz 95+ (carpan 0.01 ile) → %95 sans (tavan)

### Savas Kazanma Sansi

```
winChance = clamp(baseWinChance - invasionDifficulty + (supportStat / 100) * supportWinBonus, minWinChance, maxWinChance)
```

| Senaryo | invasionDifficulty | supportStat | Sonuc |
|---------|--------------------|-------------|-------|
| Kolay ulke, tam destek | 0.1 | 100 | **%90** |
| Zor ulke, dusuk destek | 0.4 | 20 | **%10** |
| Orta ulke, orta destek | 0.25 | 50 | **%43.75** |

### Normal Kazanma Odulu

```
effectiveBaseReward = baseReward + bonusRewards[country]
reward = effectiveBaseReward * rewardMultiplier * (supportStat / 100) * supportRewardRatio - accumulatedCostModifier
```

- `rewardMultiplier`: Event'lerdeki `baseRewardReduction` secenekleriyle carpilarak azalir (1.0'dan baslar)
- `supportRewardRatio` (0.8): Support 100 olsa bile baseReward'in max %80'i alinir

### Anlasma Odulu

```
reward = effectiveBaseReward * rewardMultiplier * dealRewardRatio - accumulatedCostModifier
```

Zar atilmaz, garanti odul verilir.

### Kose Kapma Odulu (savas kazanilirsa)

```
grabRatio = cornerGrabStat / 100
reward = effectiveBaseReward * rewardMultiplier * grabRatio * supportRewardRatio - accumulatedCostModifier
rivalShare = effectiveBaseReward * (1 - grabRatio)  → rakip ulkenin bonusRewards'una eklenir
```

### Kaybetme Cezasi

```
wealthChange = -(warLossPenalty + accumulatedCostModifier)
suspicionChange = warLossSuspicionIncrease + accumulatedSuspicionModifier
politicalInfluenceChange = -warLossPoliticalPenalty + accumulatedPoliticalInfluenceModifier
```

- Savas kaybedilirse **minigame kalici olarak kapanir** (bir daha oynamaz)

### Ateskes

Savas sirasinda oyuncu `supportStat >= ceasefireMinSupport` ise ateskes talep edebilir.

```
ratio = (supportStat - ceasefireMinSupport) / (100 - ceasefireMinSupport)
wealthChange = lerp(-ceasefirePenalty, effectiveBaseReward * rewardMultiplier * ceasefireMaxReward, ratio) - accumulatedCostModifier
```

| supportStat | ratio | Sonuc |
|-------------|-------|-------|
| 40 | 0.0 | Zararli |
| 70 | 0.5 | Basabas |
| 100 | 1.0 | Max kar |

**Kurallar:**
- supportStat < 40 → ateskes **kullanilamaz**
- Ulke fethedilmez (tekrar saldiriabilir)
- Minigame **kapanmaz**
- Cooldown baslar

### Toplum Tepkisi Ateskesi

Toplum tepkisi stat'i `protestFailThreshold`'u astiginda otomatik tetiklenir. Normal ateskes formulunu kullanir ancak:
- Oyuncu **talep etmez**, otomatik tetiklenir
- `wasProtestCeasefire = true` olarak isaretlenir
- `ceasefireMinSupport` kontrolu yapar (ratio negatife dusmemesi icin clamp edilir)

---

## Ulke Rotasyonu

UI'da ayni anda `visibleCountryCount` (varsayilan 3) ulke gosterilir. Her `rotationInterval` (varsayilan 90 sn) saniyede bir tanesi degistirilir.

**Kurallar:**
- Iste giren her ulke en az 1 rotasyon suresi boyunca korunur (swap edilemez)
- Secili ulke (aktif savas/baski) swap edilemez
- Isgal edilmis (conquered) ulkeler havuzdan cikarilir
- Havuzda yeterli ulke yoksa swap yapilmaz
- UI acip kapatmak listeyi degistirmez — rotasyon state'ten bagimsiz calisir

---

## Manager API

### UI'in Cagirdigi Metodlar

| Metod | Parametre | Ne Yapar |
|-------|-----------|----------|
| `SelectCountry(country)` | WarForOilCountry | Ulke secip PressurePhase'e gecer. Unlock, cooldown, conquered kontrolleri yapar. |
| `AttemptPressure()` | — | Baski denemesi. Basarili → savas baslar. Basarisiz → cooldown. |
| `CancelPressure()` | — | Baskidan vazgecip Idle'a doner. |
| `ResolveEvent(choiceIndex)` | int | Event secimi yapar, modifier'lari uygular. Zincir/rakip/normal akisa gore dallanir. |
| `RequestCeasefire()` | — | Ateskes talep eder. supportStat >= minSupport gerekir. |
| `DismissResultScreen()` | — | Sonuc ekranini kapatir, stat'lari uygular, cooldown baslatir. |

### Events (UI Dinleyecek)

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| `OnCountrySelected` | WarForOilCountry | Ulke secildi |
| `OnPressureResult` | bool, float | Baski sonucu (basari, cooldown suresi) |
| `OnPressureCooldownUpdate` | float | Cooldown geri sayimi (her frame) |
| `OnWarStarted` | WarForOilCountry, float | Savas baslamis (ulke, sure) |
| `OnWarProgress` | float | Savas ilerlemesi (0-1) |
| `OnWarEventTriggered` | WarForOilEvent | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | float | Event karar sayaci |
| `OnWarEventResolved` | WarForOilEventChoice | Secim yapildi |
| `OnCeasefireResult` | WarForOilResult | Ateskes sonucu |
| `OnWarResultReady` | WarForOilResult | Sonuc hazir, ekran goster |
| `OnWarFinished` | WarForOilResult | Sonuc ekrani kapandi, her sey bitti |
| `OnActiveCountriesChanged` | List\<WarForOilCountry\> | Ulke listesi degisti |
| `OnChainStarted` | — | Zincir basladi (UI savas timer'i dondurabilir) |
| `OnChainEnded` | string | Zincir bitti (sebep: "collapse", "ceasefire", "government_collapse") |
| `OnRivalInvasionStarted` | WarForOilCountry | Rakip isgal tetiklendi (rakip ulke bilgisi) |
| `OnCornerGrabStarted` | — | Kose kapma yarisi basladi (anlasma reddedildi) |
| `OnCornerGrabStatChanged` | float | Kose kapma stat'i degisti (0-100) |
| `OnProtestForeshadow` | — | Feed savas karsiti gonderilere dondu (foreshadowing) |
| `OnProtestStarted` | — | Toplum tepkisi basladi |
| `OnProtestStatChanged` | float | Toplum tepkisi stat'i degisti (0-100) |
| `OnProtestSuppressed` | — | Toplum tepkisi basariyla bastirildi |
| `OnVandalismLevelChanged` | VandalismLevel | Vandalizm seviyesi degisti |
| `OnVandalismDamage` | float | Vandalizm hasar tick'i (miktar) |
| `OnMediaPursuitLevelChanged` | MediaPursuitLevel | Medya takibi seviyesi degisti |
| `OnMediaPursuitTick` | float, float | Medya takibi tick'i (reputationLoss, suspicionGain) |

### Getter'lar

| Metod | Donus | Aciklama |
|-------|-------|----------|
| `IsActive()` | bool | Minigame aktif mi (Idle degilse true) |
| `IsPermanentlyDisabled()` | bool | Kalici devre disi mi |
| `IsCountryConquered(country)` | bool | Ulke isgal edilmis mi |
| `CanRequestCeasefire()` | bool | Ateskes talep edilebilir mi |
| `IsInChain()` | bool | Zincir aktif mi |
| `IsCornerGrabRace()` | bool | Kose kapma yarisi aktif mi |
| `GetCurrentState()` | WarForOilState | Mevcut durum |
| `GetSelectedCountry()` | WarForOilCountry | Secili ulke |
| `GetSupportStat()` | float | Destek degeri |
| `GetCornerGrabStat()` | float | Kose kapma stat degeri (0-100) |
| `GetRivalCountry()` | WarForOilCountry | Rakip ulke (varsa) |
| `GetBonusReward(country)` | float | Ulkenin bonus reward'i |
| `GetActiveCountries()` | List\<WarForOilCountry\> | UI'daki ulke listesi |
| `GetWarProgress()` | float | Savas ilerlemesi (0-1) |
| `IsProtestActive()` | bool | Toplum tepkisi aktif mi |
| `GetProtestStat()` | float | Toplum tepkisi degeri (0-100) |
| `GetVandalismLevel()` | VandalismLevel | Mevcut vandalizm seviyesi |
| `GetMediaPursuitLevel()` | MediaPursuitLevel | Mevcut medya takibi seviyesi |

---

## Oyun Duraklama Davranisi

| Durum | Oyun Durumu | Timer Tipi |
|-------|-------------|------------|
| WarProcess | **Devam ediyor** | `Time.deltaTime` |
| EventPhase | **Duraklatilmis** | `Time.unscaledDeltaTime` |
| ResultPhase | **Duraklatilmis** | Timer yok (UI bekleniyor) |
| PressurePhase | **Devam ediyor** | `Time.deltaTime` |

---

## EventCoordinator Entegrasyonu

### Cooldown (Kisa Sureli)
Savas sirasinda event tetiklemeden once `EventCoordinator.CanShowEvent()` kontrol edilir. Event tetiklendiginde `EventCoordinator.MarkEventShown()` cagirilir → diger sistemler kisa bir cooldown suresince event gonderemez.

**Not:** Zincir sistemi artik diger sistemleri kilitlemiyor. Rival invasion, protest ve media pursuit chain aktifken de calismaya devam eder.

---

## Inspector (Custom Editor)

`WarForOilEventEditor.cs` dosyasi Inspector'da kolay event duzenleme saglar:

- **ChainRole dropdown**: Sadece None/Head secenekleri. Dallanma choice seviyesinde tanimlanir.
- **isRepeatable tiklenince** "Sinirsiz Tekrar" toggle + maxRepeatCount gosterilir (sinirsiz tikli degilse)
- **Choice'lar foldout ile**: Her choice icinde 10 foldout grubu:
  1. **Modifiers** — supportModifier, suspicionModifier, reputationModifier, politicalInfluenceModifier, costModifier (birikimli), wealthModifier (anlik), cornerGrabModifier
  2. **Protest Etkisi** — protestModifier, protestTriggerChanceBonus, olasilikli tepki (hasProtestChance + alt alanlari)
  4. **Diger Sonuclar** — endsWar, reducesReward, endsWarWithDeal, blocksEvents, blocksCeasefire, blocksEventGroup, hasImmediateEvent, hasProbabilisticRewardReduction, hasProbabilisticWarEnd
  5. **Feed Sonuclari** — freezesFeed, slowsFeed, hasFeedOverride (alt kosullu alanlarla)
  6. **Zincir Dallanmasi** — chainInfluenceStat, esik degerleri, chainBranches listesi (targetEvent, weightRange0-3), chainCanEnd, chainEndWeight
  7. **Rakip Isgal Flagleri** — acceptsRivalDeal, rejectsRivalDeal
  8. **Vandalizm Etkisi** — affectsVandalism, vandalismChangeType, vandalismTargetLevel/vandalismLevelDelta (kosullu)
  9. **Medya Takibi Etkisi** — affectsMediaPursuit, mediaPursuitChangeType, mediaPursuitTargetLevel/mediaPursuitLevelDelta (kosullu)
  10. **On Kosullar** — requiredSkills, statConditions

---

## Tipik Oyun Akislari

### Normal Savas

1. **Rotasyon calisir** — UI'da 3 ulke gosterilir
2. **Oyuncu ulke secer** → PressurePhase
3. **Oyuncu baski yapar** → basarisizsa cooldown, basariliysa savas baslar
4. **Savas sureci** — 5 dakika, her 15 sn'de event kontrolu
5. **Event gelir** → oyun durur, oyuncu secer → modifier'lar uygulanir
6. **Savas biter** → olasilik kontrolu → kazanma/kaybetme
7. **Sonuc ekrani** → stat'lar uygulanir

### Zincir Event Akisi

1. Normal savas sirasinda Head event tetiklenir → zincir baslar
2. Oyuncu choice secer → choice'un `chainInfluenceStat`, esikler ve `chainBranches` kaydedilir
3. 3'lu dongude chain slotu geldiginde stat'in mevcut yuzdesi hesaplanir → aralik belirlenir → o araligin agirliklariyla secim yapilir
4. JustLuck modunda aralik yok, sadece weightRange0 kullanilir
5. Secilen event tetiklenir → oyuncu yeni choice secer → yeni dallanma (her choice farkli stat/esik/agirlik kullanabilir)
6. `chainBranches` bos olan choice secilirse → chain dogal olarak biter (ceza yok)
7. Savas biterse (timer, endsWar, ateskes) → chain de biter (ceza yok)
8. Chain aktifken rival invasion, protest, media pursuit normal calismaya devam eder

### Rakip Isgal Akisi

1. Savas 60+ saniye surdukten sonra %30 ihtimalle rakip isgal tetiklenir
2. Rakip ulke havuzdan secilir, `rivalOfferEvent` gosterilir
3. **Kabul** → %60 odul oyuncuya, %40 rakip ulkeye eklenir, savas hizla biter, **event gelmez**
4. **Red** → kose kapma yarisi baslar, eventler cornerGrabEvents'ten gelir
5. cornerGrabStat event secimlerine gore degisir
6. Savas sonunda cornerGrabStat'a gore odul bolunur

### Toplum Tepkisi Akisi

1. Savas 90+ saniye surdukten sonra %25 ihtimalle toplum tepkisi tetiklenir
2. **Faz 1** — `OnProtestForeshadow` tetiklenir, feed savas karsiti gonderilere doner, event gosterilmez
3. **Faz 2** — Bir sonraki event check'te `protestTriggerEvent` gosterilir, `protestStat` 30'dan baslar
4. Bundan sonra normal + protest event havuzlari birlestirilir, her check'te ikisinden biri gelebilir
5. Oyuncu choice'larla `protestStat`'i etkiler — her choice ayrica sonraki drift hizini belirler
6. Pasif drift: her 3 saniyede son modifier / 10 kadar stat kayar (+3 choice → +0.3/tick, -5 choice → -0.5/tick)
7. **Basarisizlik**: protestStat >= 80 → otomatik ateskes (`wasProtestCeasefire = true`)
8. **Basari**: protestStat < 10 → tepki bastirildi, protest eventleri durur, savas normal devam eder

### Medya Takibi Akisi

1. Savas 120+ saniye surdukten sonra %20 ihtimalle medya takibi tetiklenir (protest'ten bagimsiz)
2. `mediaPursuitPending = true` → sonraki event check'te `mediaPursuitTriggerEvent` gosterilir
3. `currentMediaPursuitLevel` atanir (varsayilan Low), periyodik itibar kaybi + suphe artisi baslar
4. Aktif state'in event havuzu normal havuza eklenir (Low → level1Events, Medium → level2Events, High → level3Events)
5. Oyuncu choice'larla veya event tetikleme ile medya takibi seviyesini degistirir
6. High seviyede tick basi 4 itibar kaybi + 3 suphe artisi → suphe 100'e ulasirsa game over
7. **Kurtulma**: Choice ile `Ended`'a cekilir
8. **Savas biterse**: Otomatik sifirlanir

---

## Olasilikli Savas Bitirme

Choice'ta `hasProbabilisticWarEnd` tikli ise, secenek secildiginde 3 sonuctan biri olasiliga gore gerceklesir:

| Sonuc | Aciklama |
|-------|----------|
| **Savas Biter** | `endsWar` ile ayni davranis — `warTimer` ilerletilir, `eventsBlocked = true` |
| **Event Yok Olur** | Event bu savas boyunca bir daha asla tetiklenmez, savas devam eder |
| **Tekrar Tetiklenir** | Event bir sonraki event check zamaninda kendini tekrar tetikler |

### Olasilik Olcekleme (Support Bazli)

Inspector'dan girilen olasiliklar `support = 50` icin gecerli base degerlerdir.

```
supportDelta = (support - 50) / 50     // -1..+1

adjWarEnd    = probWarEndChance  * clamp(1 - supportDelta, 0, 1)
adjDismiss   = probDismissChance * clamp(1 + supportDelta, 0, 1)
adjRetrigger = 1 - probWarEndChance - probDismissChance   (base sabit)

Normalize: total = adjWarEnd + adjDismiss + adjRetrigger
```

| Support | Etki |
|---------|------|
| 0 | Savas bitme 2x artar, yok olma 0'a duser |
| 50 | Tam base degerler |
| 100 | Savas bitme 0'a duser, yok olma 2x artar |

### Dismiss ve Retrigger

- **Dismiss**: Event id'si `dismissedEventIds` set'ine eklenir. Tum havuzlarda (normal, protest, medya takibi) filtrelenir.
- **Retrigger**: Event `forcedNextEvent` olarak kaydedilir. Sonraki TryTriggerWarEvent cagirisinda havuz secimi atlanarak direkt tetiklenir (sayac artirilmaz).
- **Savas bitis**: `eventsBlocked = true` + `warTimer` ilerletilir (probWarEndDelay kadar gecikme).
- **Alt zincir dallanma engeli**: `blocksSubChainBranching` acik event tetiklendiginde id'si `blockedBranchEventIds`'ye eklenir. Dallanma seciminde bu event'lerin agirligisifirlanir, hedef olarak secilemez. Alt agaci da erisilemez hale gelir (Head alt eventler bagimsiz tetiklenebilir).

---

## Olasilikli Odul Dusurme

Choice'ta `hasProbabilisticRewardReduction` tikli ise, secenek secildiginde 3 sonuctan biri olasiliga gore gerceklesir:

| Sonuc | Aciklama |
|-------|----------|
| **Event Tekrar Tetiklenir** | Event `forcedNextEvent` olarak kaydedilir, sonraki event check'te direkt tetiklenir |
| **Odul Duser** | `rewardMultiplier *= (1 - probRewardReductionAmount)` — carpimsal dusus. Event `dismissedEventIds`'ye eklenir |
| **Hicbir Sey Olmaz** | Event `dismissedEventIds`'ye eklenir, odul degismez |

### Olasilik Hesabi

```
roll = Random.value   (0-1 arasi zar)

if roll < probRetriggerChance → event tekrar tetiklenir
else if roll < probRetriggerChance + probRewardReductionChance → odul duser
else → hicbir sey olmaz
```

Inspector'da "Hicbir sey olmama" yuzdesini HelpBox gosterir: `%{(1 - retrigger - reduction) * 100}`

### Odul Dusme Mekanigi

`rewardMultiplier` carpimsal olarak uygulanir. Birden fazla dusme olursa birikimli etki yapar:
- 1. dusme (%30): `rewardMultiplier = 1.0 * 0.7 = 0.7`
- 2. dusme (%30): `rewardMultiplier = 0.7 * 0.7 = 0.49`

---

## Kadin Sureci Sistemi

Savas sirasinda bir chain choice'u ile tetiklenen, oyuncunun obsesyon stat'ini yonettigi bagimsiz alt sistem. WarForOilManager'dan bagimsiz calisan ayri bir WomanProcessManager singleton'i vardir.

### Temel Ozellikler

- **Tek seferlik**: Oyun boyunca yalnizca 1 kez baslatilabilir (`wasTriggeredThisGame` flag)
- **Savas disinda da devam eder**: Savas biterse surec sona ermez, RandomEventManager event'lerini sayarak devam eder
- **Kendi stat'i**: `womanObsession` (0-100, varsayilan baslangic 40)
- **3 kademe**: Obsesyon degerine gore farkli event havuzlari ve sikliklari
- **Game over**: Obsesyon 100'e ulasirsa suspicion 100'e cikarilarak game over tetiklenir
- **Surec bitis**: Obsesyon `endThreshold` (10) altina duserse surec biter
- **Havuz yonlendirme**: Bir choice'taki `redirectsWomanPool` ile kadin sureci kalici olarak baska bir WomanProcessDatabase'e yonlendirilebilir — bundan sonra eventler o database'den cekilir

### Kademeler

| Kademe | Aralik | Varsayilan Siklik | Aciklama |
|--------|--------|-------------------|----------|
| 1 | 0 - tier1Max (30) | Her 5 eventte 1 | Dusuk obsesyon, seyrek event |
| 2 | tier1Max - tier2Max (65) | Her 3 eventte 1 | Orta obsesyon |
| 3 | tier2Max - 100 | Her 2 eventte 1 | Yuksek obsesyon, sik event |

Her kademe icin "N eventte M kadin eventi" ayarlanabilir. Ornek: tier2Frequency=3, tier2WomanCount=2 → her 3 eventte 2 kadin eventi art arda tetiklenir.

### Baslatma

1. Savas icinde bir WarForOilEventChoice'ta `startsWomanProcess = true` tiklanir
2. WarForOilManager.ResolveEvent icinde `WomanProcessManager.Instance.StartProcess()` cagirilir
3. `womanObsession = initialObsession`, surec aktif olur

### Event Sayma

- **Savas icinde**: `WarForOilManager.OnWarEventResolved` dinlenir, her savas event cozumlemesinde sayac artar
- **Savas disinda**: `RandomEventManager.OnEventTriggered` dinlenir, her random event tetiklemesinde sayac artar
- Sayac kademenin `frequency` degerine ulasinca kadin eventi tetiklenir

### Event Yasaklama (Dismissed)

Kadin sureci eventleri `blockedWomanProcessEvents` listesi tasiyabilir. Bir event tetiklendiginde:
1. Listedeki eventler `dismissedWomanEvents` set'ine eklenir
2. Havuz seciminde dismissed eventler filtrelenir (pool'dan cikarilir)
3. Zincir dallanmasinda dismissed event'in agirligi 0 olur, diger dallara kayar
4. Dismissed bir Head event pool'dan secilemez → o zincir hic baslamaz
5. Tum dallar dismissed ise zincir biter, tier havuzundan devam edilir

Inspector'da `Kadin Sureci Eventi` tiklenince altta `Yasaklanan Eventler` listesi gorunur.

### Oncu Event Sistemi

Bir kadin sureci eventine oncu event baglanabilir. Akis:

1. Kadin eventi havuzdan secilir
2. Oncu event varsa once o tetiklenir (oyun durur, oyuncu secer)
3. Oncu event cozulunce oyun devam eder
4. 4 saniye sonra asil kadin sureci eventi tetiklenir

**Oncu event tipleri:**
- `WarForOil`: Oncu event bir war for oil eventidir. Savas yoksa bu kadin eventi ve oncusu ikisi de tetiklenmez (havuzdan filtrelenir).
- `RandomEvent`: Oncu event bir random eventtir. Savas durumundan bagimsiz calisir.

**Oncu event etkileri:**
- War for oil oncu: `suspicionModifier`, `reputationModifier`, `politicalInfluenceModifier`, `wealthModifier`, `womanObsessionModifier`, feed etkileri uygulanir. Savas aktifse savas-spesifik etkiler de uygulanir.
- Random event oncu: `RandomEventManager.SelectChoice()` ile tum efektler uygulanir.

**State akisi:** `Active → PrecursorPhase → PrecursorDelay (4sn) → EventPhase → Active`

### Event Cozumleme

Kadin eventleri `WarForOilEvent` altyapisini kullanir ama WomanProcessManager kendi cozumler:

| Alan | Uygulanir mi |
|------|-------------|
| `womanObsessionModifier` | Evet — obsesyon stat'i guncellenir |
| `suspicionModifier` | Evet — GameStatManager |
| `reputationModifier` | Evet — GameStatManager |
| `politicalInfluenceModifier` | Evet — GameStatManager |
| `wealthModifier` | Evet — anlik para |
| Feed etkileri | Evet — freeze/slow/override |
| `permanentMultipliers` | Evet — kalici stat carpanlari |
| `redirectsWomanPool` | Evet — kalici havuz yonlendirme (baska WomanProcessDatabase'e gecer) |
| `supportModifier` | Kosullu — savas aktifse WarForOilManager'a iletilir, degilse yok sayilir |
| `cornerGrabModifier` | Kosullu — savas aktif + kose kapma yarisi varsa uygulanir |
| Protest etkileri | Kosullu — savas aktif + protest aktifse uygulanir |
| Vandalizm etkileri | Kosullu — savas aktifse uygulanir |
| Medya takibi etkileri | Kosullu — savas aktifse uygulanir |
| `costModifier` | Kosullu — savas aktifse biriktirilir |
| `endsWar`, `blocksEvents` vb. | Hayir — yapisal kontroller uygulanmaz |

### WomanProcessDatabase Ayarlari

| Alan | Varsayilan | Aciklama |
|------|-----------|----------|
| `initialObsession` | 40 | Baslangic obsesyon degeri |
| `endThreshold` | 10 | Bu degerin altina duserse surec biter |
| `tier1Max` | 30 | Kademe 1 ust siniri |
| `tier2Max` | 65 | Kademe 2 ust siniri |
| `tier1Events` | — | Kademe 1 event havuzu |
| `tier2Events` | — | Kademe 2 event havuzu |
| `tier3Events` | — | Kademe 3 event havuzu |
| `tier1Frequency` | 5 | Kademe 1'de her N eventte |
| `tier1WomanCount` | 1 | Kademe 1'de dongu basina kac kadin eventi |
| `tier2Frequency` | 3 | Kademe 2'de her N eventte |
| `tier2WomanCount` | 1 | Kademe 2'de dongu basina kac kadin eventi |
| `tier3Frequency` | 2 | Kademe 3'te her N eventte |
| `tier3WomanCount` | 1 | Kademe 3'te dongu basina kac kadin eventi |
| `decisionTime` | 10 | Karar suresi (saniye) |

### Inspector Kullanimi

**Event olusturma:**
1. `Kadin Sureci Eventi` tiklanir → event kadin sureci havuzuna eklenebilir
2. Altinda `Yasaklanan Eventler` listesi gorunur — bu event tetiklenince listedeki eventler havuzdan/zincirlerden cikarilir
3. Choice'larda Modifiers foldout'unda `Kadin Obsesyonu` alani gorunur

**Sureci baslatma/bitirme:**
1. Herhangi bir savas eventinin choice'unda `Diger Sonuclar` → `Kadin Sureci` → `Baslat` veya `Bitir` butonlarindan biri tiklanir (ayni anda ikisi secilemez)

### Eventler (UI Dinleyecek)

| Event | Aciklama |
|-------|----------|
| `OnWomanProcessStarted` | Surec basladi |
| `OnObsessionChanged(float)` | Obsesyon degeri degisti |
| `OnWomanEventTriggered(WarForOilEvent)` | Kadin eventi tetiklendi |
| `OnWomanEventDecisionTimerUpdate(float)` | Karar sayaci guncellendi |
| `OnWomanEventResolved(WarForOilEventChoice)` | Secim yapildi |
| `OnWomanProcessEnded` | Surec bitti (obsesyon dusuk) |
| `OnWomanProcessGameOver` | Game over (obsesyon 100) |

---

## Dosya Yapisi

```
Assets/Scripts/Minigames/WarForOil/
├── WarForOilCountry.cs         — ulke verisi (ScriptableObject)
├── WarForOilEvent.cs           — event + choice + ChainRole + ChainBranch
├── WarForOilDatabase.cs        — ayarlar + event havuzlari (ScriptableObject)
├── WarForOilManager.cs         — ana mantik + state machine + rotasyon (MonoBehaviour)
├── WomanProcessDatabase.cs     — kadin sureci ayarlari (ScriptableObject)
├── WomanProcessManager.cs      — kadin sureci mantigi (MonoBehaviour, Singleton)
├── Editor/
│   └── WarForOilEventEditor.cs — Inspector custom editor
└── warforoil-readme.md         — bu dosya
```

---

## Hikaye Bayraklari Sistemi (StoryFlag)

Oyun icinde gerceklesmis onemli olaylari takip eden bayrak sistemi. Bir kez aktif edilen bayrak oyun boyunca kalicidir.

### Dosyalar

- `Assets/Scripts/Core/Enums/StoryFlag.cs` — enum tanimi (tum bayraklar burada)
- `Assets/Scripts/Core/StoryFlagManager.cs` — singleton manager (HashSet ile takip)

### Kullanim

**Bayrak aktif etme (choice tarafinda):**
- `WarForOilEventChoice.setsStoryFlags` — bu choice secildiginde listedeki bayraklar aktif olur
- Inspector'da "Diger Sonuclar" foldout'unda "Hikaye Bayraklari" olarak gozukur

**Aciklama degistirme (event tarafinda):**
- `WarForOilEvent.conditionalDescriptions` — hikaye bayragi aktifse default aciklama yerine alternatif metin gosterilir
- `GetDescription()` metodu ile alinir — ilk eslesen bayrak gecerli olur
- Inspector'da "Kosullu Aciklamalar" olarak gozukur
