# Feed System

Oyun boyunca akan sosyal medya yorumlarını yöneten sistem.

## Nasıl Çalışır?

### Doğal Akış
- Her 40-150 saniyede bir "trend topic" belirlenir
- Feed'deki yorumların %28-45'i bu trend topic hakkında olur
- Geri kalanı rastgele diğer konulardan gelir
- Her 1-6 saniyede bir yeni yorum düşer

### Oyuncu Müdahalesi (Topic)
- Belirli skill'ler açıldığında oyuncu feed'in konusunu değiştirebilir
- Manipüle edilen topic feed'in %70-90'ını kaplar
- Bu etki belirli bir süre sonra biter ve doğal akışa dönülür

### Oyuncu Müdahalesi (Hız)
- Belirli skill'ler ile yorum akış hızı artırılabilir
- Örnek: "Agresif Bot Bas" → Yorumlar saniyede bir düşer
- Minimum hız 1 saniyenin altına inemez
- Etki belirli süre sonra biter

### Oyuncu Müdahalesi (Dondurma/Yavaşlatma)
- Bazı skill'ler feed'i dondurma veya yavaşlatma YETENEĞİ verir
- Yetenek açıldıktan sonra oyuncu istediği zaman kullanabilir
- Dondurma: Feed tamamen durur (yeni yorum gelmez)
- Yavaşlatma: Yorumlar daha yavaş gelir
- Aynı anda sadece biri aktif olabilir

### Event Etkisi
- Bazı event seçimleri feed'in konusunu değiştirebilir
- Bazı event seçimleri feed'in hızını değiştirebilir
- Her event farklı oranda ve sürede etki yaratabilir

### Hassas Konu (Sensitive Topic)
- Her oyun başında ülkenin "hassas konusu" rastgele belirlenir
- Hassas konunun trend olma şansı farklı değil
- Ancak hassas konuda yapılan manipülasyonlar daha etkili olacak

## Topic'ler
Tax, Politics, Scandal, StockMarket, Technology, RealEstate, Banking, Celebrity, Sports, General, Militarism, WarJustification, AntiWarSpeech

## Özet
```
Doğal Akış                    Müdahale
├── Topic: %28-45       →     Topic Override: %70-90
├── Hız: 1-6 sn         →     Speed Boost: min 1 sn
│                       →     Slow: 8-15 sn
│                       →     Freeze: tamamen durur
└── Arka planda döner         Skill yetenek verir, oyuncu kullanır
```
